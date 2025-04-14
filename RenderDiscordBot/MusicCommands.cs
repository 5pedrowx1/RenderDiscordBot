using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using DSharpPlus.Interactivity.Extensions;
using SpotifyAPI.Web;

namespace RenderDiscordBot
{
    public class MusicCommands : BaseCommandModule
    {
        private static Config _config = null!;
        private static readonly Dictionary<ulong, PlaylistState> _playlists = [];
        private static readonly Dictionary<ulong, CancellationTokenSource> _interactionTokens = [];

        private class QueuedTrack
        {
            public required LavalinkTrack Track { get; set; }
            public ulong AddedBy { get; set; }
        }

        private class PlaylistState
        {
            public List<QueuedTrack> Tracks { get; set; } = [];
            public int CurrentIndex { get; set; } = 0;
            public DateTime TrackStartTime { get; set; } = DateTime.UtcNow;
            public CancellationTokenSource? MonitorTokenSource { get; set; }
            public DiscordMessage? CurrentMessage { get; set; }
        }

        private static async Task EnsureConfigLoadedAsync()
        {
            _config ??= await ConfigService.GetConfigFromFirestoreAsync();
        }

        private async Task<bool> ValidateCommandUsage(CommandContext ctx)
        {
            await EnsureConfigLoadedAsync();

            if (ctx.Channel.Id != _config.ComandsChannelId)
            {
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Canal de Comandos Inválido",
                    Description = "Por favor, utilize o canal de comandos designado para interagir com o bot.",
                    Color = DiscordColor.Red
                };
                await ctx.Channel.SendMessageAsync(embed: embed);
                return false;
            }

            if (ctx.Member?.VoiceState == null || ctx.Member.VoiceState.Channel == null)
            {
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Canal de Voz Não Encontrado",
                    Description = "Por favor, entre no canal de voz designado para reprodução de músicas.",
                    Color = DiscordColor.Red
                };
                await ctx.Channel.SendMessageAsync(embed: embed);
                return false;
            }

            if (ctx.Member.VoiceState.Channel.Id != _config.MusicVoiceId)
            {
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Canal de Voz Inválido",
                    Description = "Por favor, entre no canal de voz designado para reprodução de músicas.",
                    Color = DiscordColor.Red
                };
                await ctx.Channel.SendMessageAsync(embed: embed);
                return false;
            }

            return true;
        }

        [Command("play")]
        public async Task PlayMusic(CommandContext ctx, [RemainingText] string query)
        {
            if (!await ValidateCommandUsage(ctx))
                return;

            await ctx.Message.DeleteAsync();

            var userVC = ctx.Member!.VoiceState.Channel;
            var lavalinkInstance = ctx.Client.GetLavalink();

            if (!lavalinkInstance.ConnectedNodes.Any())
            {
                await ctx.Channel.SendMessageAsync("Conexão não estabelecida!");
                return;
            }

            if (userVC.Type != ChannelType.Voice)
            {
                await ctx.Channel.SendMessageAsync("Por favor, conecte-se ao canal de voz de Música!");
                return;
            }

            var node = lavalinkInstance.ConnectedNodes.Values.First();
            await node.ConnectAsync(userVC);

            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
            if (conn == null)
            {
                await ctx.Channel.SendMessageAsync("Lavalink falhou ao conectar!");
                return;
            }

            var searchQuery = await node.Rest.GetTracksAsync(query);
            if (searchQuery.LoadResultType == LavalinkLoadResultType.NoMatches ||
                searchQuery.LoadResultType == LavalinkLoadResultType.LoadFailed)
            {
                await ctx.Channel.SendMessageAsync($"Falha ao encontrar música com consulta: {query}");
                return;
            }

            var musicTrack = searchQuery.Tracks.First();

            if (conn.CurrentState.CurrentTrack != null)
            {
                if (!_playlists.ContainsKey(ctx.Guild.Id))
                {
                    _playlists[ctx.Guild.Id] = new PlaylistState();
                    _playlists[ctx.Guild.Id].Tracks.Add(new QueuedTrack
                    {
                        Track = conn.CurrentState.CurrentTrack,
                        AddedBy = ctx.User.Id
                    });
                }
                _playlists[ctx.Guild.Id].Tracks.Add(new QueuedTrack
                {
                    Track = musicTrack,
                    AddedBy = ctx.User.Id
                });

                var queueEmbed = new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Blurple,
                    Title = "Música Adicionada à Fila",
                    Description = $"**{musicTrack.Title}** foi adicionada à fila por {ctx.User.Username}.",
                    Timestamp = DateTime.UtcNow
                };

                var queueMessage = await ctx.Channel.SendMessageAsync(embed: queueEmbed);
                await Task.Delay(TimeSpan.FromSeconds(10));
                await queueMessage.DeleteAsync();
                return;
            }

            await conn.PlayAsync(musicTrack);
            var trackStartTime = DateTime.UtcNow;

            string? artworkUrl = null;
            if (musicTrack.Uri != null && musicTrack.Uri.Host.Contains("youtube.com"))
            {
                var videoId = ExtractYouTubeVideoId(musicTrack.Uri.ToString());
                if (videoId != null)
                    artworkUrl = $"https://img.youtube.com/vi/{videoId}/0.jpg";
            }

            var nowPlayingEmbed = new DiscordEmbedBuilder
            {
                Color = DiscordColor.Purple,
                Title = "🎵 Agora Tocando",
                Description = $"🎶 **{musicTrack.Title}**\n" +
                              $"📝 **Autor:** {musicTrack.Author}\n" +
                              $"🔗 **[Ouvir no navegador]({musicTrack.Uri})**\n" +
                              $"👤 **Adicionado por:** {ctx.User.Username}",
                Thumbnail = artworkUrl != null ? new DiscordEmbedBuilder.EmbedThumbnail { Url = artworkUrl } : null,
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = "La City - A sua trilha sonora de sempre!"
                },
                Timestamp = DateTime.UtcNow
            };

            var messageBuilder = new DiscordMessageBuilder()
                .WithEmbed(nowPlayingEmbed)
                .AddComponents(
                    new DiscordButtonComponent(ButtonStyle.Primary, "pause", "⏸️ Pausa"),
                    new DiscordButtonComponent(ButtonStyle.Success, "resume", "▶️ Retomar"),
                    new DiscordButtonComponent(ButtonStyle.Primary, "next", "⏭️ Próxima"),
                    new DiscordButtonComponent(ButtonStyle.Danger, "stop", "⏹️ Parar")
                );

            var message = await ctx.Channel.SendMessageAsync(messageBuilder);

            _playlists[ctx.Guild.Id] = new PlaylistState
            {
                TrackStartTime = trackStartTime,
                CurrentIndex = 0,
                CurrentMessage = message
            };
            _playlists[ctx.Guild.Id].Tracks.Add(new QueuedTrack
            {
                Track = musicTrack,
                AddedBy = ctx.User.Id
            });

            _interactionTokens[ctx.Guild.Id] = new CancellationTokenSource();
            _ = StartInteractivityLoop(ctx, conn, _interactionTokens[ctx.Guild.Id].Token);

            conn.PlaybackFinished += async (connection, eventArgs) =>
            {
                if (_playlists.TryGetValue(ctx.Guild.Id, out var state) && state.CurrentMessage != null)
                {
                    var oldEmbed = state.CurrentMessage.Embeds[0];
                    var builder = new DiscordMessageBuilder().WithEmbed(oldEmbed);
                    builder.ClearComponents();
                    await state.CurrentMessage.ModifyAsync(builder);
                }

                if (_playlists[ctx.Guild.Id].CurrentIndex < _playlists[ctx.Guild.Id].Tracks.Count - 1)
                {
                    _playlists[ctx.Guild.Id].CurrentIndex++;
                    _playlists[ctx.Guild.Id].TrackStartTime = DateTime.UtcNow;

                    var nextQueuedTrack = _playlists[ctx.Guild.Id].Tracks[_playlists[ctx.Guild.Id].CurrentIndex];
                    await conn.PlayAsync(nextQueuedTrack.Track);

                    string? nextArtworkUrl = null;
                    if (nextQueuedTrack.Track.Uri != null && nextQueuedTrack.Track.Uri.Host.Contains("youtube.com"))
                    {
                        var videoId = ExtractYouTubeVideoId(nextQueuedTrack.Track.Uri.ToString());
                        if (videoId != null)
                            nextArtworkUrl = $"https://img.youtube.com/vi/{videoId}/0.jpg";
                    }

                    var newEmbed = new DiscordEmbedBuilder
                    {
                        Color = DiscordColor.Purple,
                        Title = "🎵 Agora Tocando",
                        Description = $"🎶 **{nextQueuedTrack.Track.Title}**\n" +
                                      $"📝 **Autor:** {nextQueuedTrack.Track.Author}\n" +
                                      $"🔗 **[Ouvir no navegador]({nextQueuedTrack.Track.Uri})**\n" +
                                      $"👤 **Adicionado por:** (ID: {nextQueuedTrack.AddedBy})",
                        Thumbnail = nextArtworkUrl != null ? new DiscordEmbedBuilder.EmbedThumbnail { Url = nextArtworkUrl } : null,
                        Footer = new DiscordEmbedBuilder.EmbedFooter
                        {
                            Text = "La City - A sua trilha sonora de sempre!"
                        },
                        Timestamp = DateTime.UtcNow
                    };

                    var newMessageBuilder = new DiscordMessageBuilder()
                        .WithEmbed(newEmbed)
                        .AddComponents(
                            new DiscordButtonComponent(ButtonStyle.Primary, "pause", "⏸️ Pausa"),
                            new DiscordButtonComponent(ButtonStyle.Success, "resume", "▶️ Retomar"),
                            new DiscordButtonComponent(ButtonStyle.Primary, "next", "⏭️ Próxima"),
                            new DiscordButtonComponent(ButtonStyle.Danger, "stop", "⏹️ Parar")
                        );

                    var newMessage = await ctx.Channel.SendMessageAsync(newMessageBuilder);
                    _playlists[ctx.Guild.Id].CurrentMessage = newMessage;

                    _interactionTokens[ctx.Guild.Id].Cancel();
                    _interactionTokens[ctx.Guild.Id] = new CancellationTokenSource();
                    _ = StartInteractivityLoop(ctx, conn, _interactionTokens[ctx.Guild.Id].Token);
                }
            };
        }

        private async Task StartInteractivityLoop(CommandContext ctx, LavalinkGuildConnection conn, CancellationToken token)
        {
            var interactivity = ctx.Client.GetInteractivity();
            while (!token.IsCancellationRequested)
            {
                if (!_playlists.TryGetValue(ctx.Guild.Id, out var state) || state.CurrentMessage == null)
                    break;

                var buttonResult = await interactivity.WaitForButtonAsync(state.CurrentMessage, token);
                if (buttonResult.Result is null)
                    continue;

                var btn = buttonResult.Result;
                var currentQueuedTrack = _playlists[ctx.Guild.Id].Tracks[_playlists[ctx.Guild.Id].CurrentIndex];
                if (btn.User.Id != currentQueuedTrack.AddedBy)
                {
                    await btn.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("Você não pode controlar essa música.")
                            .AsEphemeral());
                    continue;
                }

                switch (btn.Id)
                {
                    case "pause":
                        await conn.PauseAsync();
                        await btn.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                            new DiscordInteractionResponseBuilder()
                                .AddEmbed(state.CurrentMessage.Embeds[0])
                                .AddComponents(
                                    new DiscordButtonComponent(ButtonStyle.Primary, "pause", "⏸️ Pausa", disabled: true),
                                    new DiscordButtonComponent(ButtonStyle.Success, "resume", "▶️ Retomar"),
                                    new DiscordButtonComponent(ButtonStyle.Primary, "next", "⏭️ Próxima"),
                                    new DiscordButtonComponent(ButtonStyle.Danger, "stop", "⏹️ Parar")
                                ));
                        break;

                    case "resume":
                        await conn.ResumeAsync();
                        await btn.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                            new DiscordInteractionResponseBuilder()
                                .AddEmbed(state.CurrentMessage.Embeds[0])
                                .AddComponents(
                                    new DiscordButtonComponent(ButtonStyle.Primary, "pause", "⏸️ Pausa"),
                                    new DiscordButtonComponent(ButtonStyle.Success, "resume", "▶️ Retomar", disabled: true),
                                    new DiscordButtonComponent(ButtonStyle.Primary, "next", "⏭️ Próxima"),
                                    new DiscordButtonComponent(ButtonStyle.Danger, "stop", "⏹️ Parar")
                                ));
                        break;

                    case "next":
                        if (_playlists[ctx.Guild.Id].CurrentIndex < _playlists[ctx.Guild.Id].Tracks.Count - 1)
                        {
                            _playlists[ctx.Guild.Id].CurrentIndex++;
                            _playlists[ctx.Guild.Id].TrackStartTime = DateTime.UtcNow;

                            var nextQueuedTrack = _playlists[ctx.Guild.Id].Tracks[_playlists[ctx.Guild.Id].CurrentIndex];
                            await conn.PlayAsync(nextQueuedTrack.Track);

                            string? nextArtworkUrl = null;
                            if (nextQueuedTrack.Track.Uri != null && nextQueuedTrack.Track.Uri.Host.Contains("youtube.com"))
                            {
                                var videoId = ExtractYouTubeVideoId(nextQueuedTrack.Track.Uri.ToString());
                                if (videoId != null)
                                    nextArtworkUrl = $"https://img.youtube.com/vi/{videoId}/0.jpg";
                            }

                            var newEmbed = new DiscordEmbedBuilder
                            {
                                Color = DiscordColor.Purple,
                                Title = "🎵 Agora Tocando",
                                Description = $"🎶 **{nextQueuedTrack.Track.Title}**\n" +
                                              $"📝 **Autor:** {nextQueuedTrack.Track.Author}\n" +
                                              $"🔗 **[Ouvir no navegador]({nextQueuedTrack.Track.Uri})**\n" +
                                              $"👤 **Adicionado por:** (ID: {nextQueuedTrack.AddedBy})",
                                Thumbnail = nextArtworkUrl != null ? new DiscordEmbedBuilder.EmbedThumbnail { Url = nextArtworkUrl } : null,
                                Footer = new DiscordEmbedBuilder.EmbedFooter
                                {
                                    Text = "La City - A sua trilha sonora de sempre!"
                                },
                                Timestamp = DateTime.UtcNow
                            };

                            var newMessageBuilder = new DiscordMessageBuilder()
                                .WithEmbed(newEmbed)
                                .AddComponents(
                                    new DiscordButtonComponent(ButtonStyle.Primary, "pause", "⏸️ Pausa"),
                                    new DiscordButtonComponent(ButtonStyle.Success, "resume", "▶️ Retomar"),
                                    new DiscordButtonComponent(ButtonStyle.Primary, "next", "⏭️ Próxima"),
                                    new DiscordButtonComponent(ButtonStyle.Danger, "stop", "⏹️ Parar")
                                );

                            var newMessage = await ctx.Channel.SendMessageAsync(newMessageBuilder);
                            _playlists[ctx.Guild.Id].CurrentMessage = newMessage;

                            _interactionTokens[ctx.Guild.Id].Cancel();
                            _interactionTokens[ctx.Guild.Id] = new CancellationTokenSource();
                            _ = StartInteractivityLoop(ctx, conn, _interactionTokens[ctx.Guild.Id].Token);
                        }
                        else
                        {
                            await btn.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                                new DiscordInteractionResponseBuilder()
                                    .WithContent("Não há mais músicas na fila.")
                                    .AsEphemeral());
                        }
                        break;

                    case "stop":
                        await conn.StopAsync();
                        await conn.DisconnectAsync();

                        var responseBuilder = new DiscordInteractionResponseBuilder()
                            .AddEmbed(state.CurrentMessage.Embeds[0]);
                        responseBuilder.ClearComponents();

                        await btn.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, responseBuilder.AsEphemeral());

                        _playlists.Remove(ctx.Guild.Id);
                        return;
                }
            }
        }

        [Command("musiclist")]
        public async Task ShowQueue(CommandContext ctx)
        {
            await ctx.Message.DeleteAsync();

            if (!_playlists.TryGetValue(ctx.Guild.Id, out var playlist) || playlist.Tracks.Count == 0)
            {
                var emptyEmbed = new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Red,
                    Title = "📜 Fila de Reprodução",
                    Description = "🎵 Não há músicas na fila no momento.",
                    Timestamp = DateTime.UtcNow
                };

                await ctx.Channel.SendMessageAsync(embed: emptyEmbed);
                return;
            }

            var queueEmbed = new DiscordEmbedBuilder
            {
                Color = DiscordColor.Blurple,
                Title = "🎶 Fila de Reprodução",
                Description = $"📀 **Total de músicas na fila:** {playlist.Tracks.Count}",
                Timestamp = DateTime.UtcNow
            };

            int maxToShow = 10;
            int start = Math.Max(0, playlist.CurrentIndex - 2);

            for (int i = start; i < Math.Min(playlist.Tracks.Count, start + maxToShow); i++)
            {
                var queuedTrack = playlist.Tracks[i];
                string status = i == playlist.CurrentIndex ? "▶️ **Tocando agora**" : $"{i + 1}.";
                string duration = $"⏳ {queuedTrack.Track.Length:mm\\:ss}";

                queueEmbed.AddField(status, $"[{queuedTrack.Track.Title}]({queuedTrack.Track.Uri}) - {queuedTrack.Track.Author} {duration}", false);
            }

            if (playlist.Tracks.Count > maxToShow)
            {
                queueEmbed.Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"📌 E mais {playlist.Tracks.Count - maxToShow} músicas na fila..."
                };
            }

            await ctx.Channel.SendMessageAsync(embed: queueEmbed);
        }

        private static string? ExtractYouTubeVideoId(string url)
        {
            if (url.Contains("youtube.com/watch?v="))
                return url.Split("v=")[1].Split('&')[0];

            if (url.Contains("youtu.be/"))
                return url.Split("youtu.be/")[1].Split('?')[0];

            return null;
        }

        [Command("playlist")]
        public async Task SpotifyPlaylist(CommandContext ctx, [RemainingText] string spotifyPlaylistUrl)
        {
            if (!await ValidateCommandUsage(ctx))
                return;

            await ctx.Message.DeleteAsync();

            string? playlistId = ExtractSpotifyPlaylistId(spotifyPlaylistUrl);
            if (string.IsNullOrEmpty(playlistId))
            {
                await ctx.Channel.SendMessageAsync("URL de playlist do Spotify inválida.");
                return;
            }

            var config = SpotifyClientConfig.CreateDefault();
            var request = new ClientCredentialsRequest(_config.CLIENT_ID, _config.CLIENT_SECRET);
            var oauthClient = new OAuthClient(config);
            var tokenResponse = await oauthClient.RequestToken(request);
            var spotify = new SpotifyClient(config.WithToken(tokenResponse.AccessToken));

            var playlist = await spotify.Playlists.Get(playlistId);
            if (playlist.Tracks!.Total == 0)
            {
                await ctx.Channel.SendMessageAsync("Não foi encontrada nenhuma faixa na playlist do Spotify.");
                return;
            }

            var playlistTracks = playlist.Tracks.Items;
            var fetchedTracks = new List<LavalinkTrack>();
            var notFoundTracks = new List<string>();

            var userVC = ctx.Member!.VoiceState.Channel;
            var lavalinkInstance = ctx.Client.GetLavalink();

            if (!lavalinkInstance.ConnectedNodes.Any())
            {
                await ctx.Channel.SendMessageAsync("Conexão não estabelecida com o Lavalink!");
                return;
            }
            if (userVC.Type != ChannelType.Voice)
            {
                await ctx.Channel.SendMessageAsync("Por favor, conecte-se ao canal de voz para reprodução de músicas!");
                return;
            }

            var node = lavalinkInstance.ConnectedNodes.Values.First();
            await node.ConnectAsync(userVC);
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
            if (conn == null)
            {
                await ctx.Channel.SendMessageAsync("Lavalink falhou ao conectar!");
                return;
            }

            if (playlistTracks!.Count > 20)
            {
                var progressEmbed = new DiscordEmbedBuilder
                {
                    Title = "🎶 Carregando as faixas da playlist...",
                    Description = $"Progresso: `0 / {playlistTracks.Count}`",
                    Color = DiscordColor.Blurple,
                    Timestamp = DateTime.UtcNow
                };

                var progressMessage = await ctx.Channel.SendMessageAsync(embed: progressEmbed);
                int processed = 0;

                foreach (var item in playlistTracks)
                {
                    processed++;

                    if (item.Track is FullTrack track)
                    {
                        string artistName = track.Artists.FirstOrDefault()?.Name ?? "";
                        string query = $"{track.Name} {artistName}";
                        var searchResult = await node.Rest.GetTracksAsync(query);

                        if (searchResult.LoadResultType == LavalinkLoadResultType.NoMatches ||
                            searchResult.LoadResultType == LavalinkLoadResultType.LoadFailed)
                        {
                            notFoundTracks.Add(query);
                            continue;
                        }

                        fetchedTracks.Add(searchResult.Tracks.First());
                    }

                    progressEmbed.Description = $"Progresso: `{processed} / {playlistTracks.Count}`";
                    await progressMessage.ModifyAsync(new DiscordMessageBuilder().WithEmbed(progressEmbed));
                }

                await progressMessage.DeleteAsync();
            }

            else
            {
                foreach (var item in playlistTracks)
                {
                    if (item.Track is FullTrack track)
                    {
                        string artistName = track.Artists.FirstOrDefault()?.Name ?? "";
                        string query = $"{track.Name} {artistName}";
                        var searchResult = await node.Rest.GetTracksAsync(query);
                        if (searchResult.LoadResultType == LavalinkLoadResultType.NoMatches ||
                            searchResult.LoadResultType == LavalinkLoadResultType.LoadFailed)
                        {
                            notFoundTracks.Add(query);
                            continue;
                        }
                        fetchedTracks.Add(searchResult.Tracks.First());
                    }
                }
            }

            if (fetchedTracks.Count == 0)
            {
                await ctx.Channel.SendMessageAsync("Nenhuma faixa compatível foi encontrada para essa playlist.");
                return;
            }

            if (_playlists.ContainsKey(ctx.Guild.Id))
            {
                foreach (var track in fetchedTracks)
                {
                    _playlists[ctx.Guild.Id].Tracks.Add(new QueuedTrack
                    {
                        Track = track,
                        AddedBy = ctx.User.Id
                    });
                }

                var embed = new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Blurple,
                    Title = "Playlist Atualizada",
                    Description = $"Foram adicionadas **{fetchedTracks.Count}** músicas da playlist do Spotify à fila.",
                    Timestamp = DateTime.UtcNow
                };

                if (notFoundTracks.Count > 0)
                    embed.AddField("Faixas não encontradas", string.Join(", ", notFoundTracks));

                var queueMessage = await ctx.Channel.SendMessageAsync(embed: embed);
                await Task.Delay(TimeSpan.FromSeconds(10));
                await queueMessage.DeleteAsync();
                return;
            }

            if (conn.CurrentState.CurrentTrack != null)
            {
                if (!_playlists.ContainsKey(ctx.Guild.Id))
                {
                    _playlists[ctx.Guild.Id] = new PlaylistState
                    {
                        TrackStartTime = DateTime.UtcNow,
                        CurrentIndex = -1,
                        Tracks = []
                    };
                }
                foreach (var track in fetchedTracks)
                {
                    _playlists[ctx.Guild.Id].Tracks.Add(new QueuedTrack
                    {
                        Track = track,
                        AddedBy = ctx.User.Id
                    });
                }
                await ctx.Channel.SendMessageAsync("Uma música já está tocando, faixas adicionadas à fila.");
                return;
            }

            await conn.PlayAsync(fetchedTracks.First());
            var trackStartTime = DateTime.UtcNow;

            conn.PlaybackFinished += async (connection, eventArgs) =>
            {
                if (_playlists.TryGetValue(ctx.Guild.Id, out var state) && state.CurrentMessage != null)
                {
                    var oldEmbed = state.CurrentMessage.Embeds[0];
                    var builder = new DiscordMessageBuilder().WithEmbed(oldEmbed);
                    builder.ClearComponents();
                    await state.CurrentMessage.ModifyAsync(builder);
                }

                if (_playlists[ctx.Guild.Id].CurrentIndex < _playlists[ctx.Guild.Id].Tracks.Count - 1)
                {
                    _playlists[ctx.Guild.Id].CurrentIndex++;
                    _playlists[ctx.Guild.Id].TrackStartTime = DateTime.UtcNow;
                    var nextQueuedTrack = _playlists[ctx.Guild.Id].Tracks[_playlists[ctx.Guild.Id].CurrentIndex];
                    await conn.PlayAsync(nextQueuedTrack.Track);

                    string? nextArtworkUrl = null;
                    if (nextQueuedTrack.Track.Uri != null && nextQueuedTrack.Track.Uri.Host.Contains("youtube.com"))
                    {
                        var videoId = ExtractYouTubeVideoId(nextQueuedTrack.Track.Uri.ToString());
                        if (videoId != null)
                            nextArtworkUrl = $"https://img.youtube.com/vi/{videoId}/0.jpg";
                    }

                    var newEmbed = new DiscordEmbedBuilder
                    {
                        Color = DiscordColor.Purple,
                        Title = "🎵 Agora Tocando",
                        Description = $"🎶 **{nextQueuedTrack.Track.Title}**\n" +
                                      $"📝 **Autor:** {nextQueuedTrack.Track.Author}\n" +
                                      $"🔗 **[Ouvir no navegador]({nextQueuedTrack.Track.Uri})**\n" +
                                      $"👤 **Adicionado por:** (ID: {nextQueuedTrack.AddedBy})",
                        Timestamp = DateTime.UtcNow
                    };
                    if (nextArtworkUrl != null)
                        newEmbed.Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail { Url = nextArtworkUrl };

                    var newMessageBuilder = new DiscordMessageBuilder()
                        .WithEmbed(newEmbed)
                        .AddComponents(
                            new DiscordButtonComponent(ButtonStyle.Primary, "pause", "⏸️ Pausa"),
                            new DiscordButtonComponent(ButtonStyle.Success, "resume", "▶️ Retomar"),
                            new DiscordButtonComponent(ButtonStyle.Primary, "next", "⏭️ Próxima"),
                            new DiscordButtonComponent(ButtonStyle.Danger, "stop", "⏹️ Parar")
                        );
                    var newMessage = await ctx.Channel.SendMessageAsync(newMessageBuilder);
                    _playlists[ctx.Guild.Id].CurrentMessage = newMessage;

                    _interactionTokens[ctx.Guild.Id].Cancel();
                    _interactionTokens[ctx.Guild.Id] = new CancellationTokenSource();
                    _ = StartInteractivityLoop(ctx, conn, _interactionTokens[ctx.Guild.Id].Token);
                }
                else
                {
                    await conn.StopAsync();
                }
            };

            string? artworkUrl = null;
            if (fetchedTracks.First().Uri != null && fetchedTracks.First().Uri.Host.Contains("youtube.com"))
            {
                var videoId = ExtractYouTubeVideoId(fetchedTracks.First().Uri.ToString());
                if (videoId != null)
                    artworkUrl = $"https://img.youtube.com/vi/{videoId}/0.jpg";
            }

            var nowPlayingEmbed = new DiscordEmbedBuilder
            {
                Color = DiscordColor.Purple,
                Title = "🎵 Agora Tocando (Spotify Playlist)",
                Description = $"🎶 **{fetchedTracks.First().Title}**\n" +
                              $"📝 **Autor:** {fetchedTracks.First().Author}\n" +
                              $"🔗 **[Ouvir no navegador]({fetchedTracks.First().Uri})**\n" +
                              $"👤 **Adicionado por:** {ctx.User.Username}",
                Thumbnail = artworkUrl != null ? new DiscordEmbedBuilder.EmbedThumbnail { Url = artworkUrl } : null,
                Footer = new DiscordEmbedBuilder.EmbedFooter { Text = "La City - A sua trilha sonora de sempre!" },
                Timestamp = DateTime.UtcNow
            };

            var messageBuilder = new DiscordMessageBuilder()
                .WithEmbed(nowPlayingEmbed)
                .AddComponents(
                    new DiscordButtonComponent(ButtonStyle.Primary, "pause", "⏸️ Pausa"),
                    new DiscordButtonComponent(ButtonStyle.Success, "resume", "▶️ Retomar"),
                    new DiscordButtonComponent(ButtonStyle.Primary, "next", "⏭️ Próxima"),
                    new DiscordButtonComponent(ButtonStyle.Danger, "stop", "⏹️ Parar")
                );

            var message = await ctx.Channel.SendMessageAsync(messageBuilder);

            var newPlaylist = new PlaylistState
            {
                TrackStartTime = trackStartTime,
                CurrentIndex = 0,
                CurrentMessage = message,
                Tracks = []
            };

            newPlaylist.Tracks.Add(new QueuedTrack { Track = fetchedTracks.First(), AddedBy = ctx.User.Id });
            foreach (var track in fetchedTracks.Skip(1))
            {
                newPlaylist.Tracks.Add(new QueuedTrack { Track = track, AddedBy = ctx.User.Id });
            }
            _playlists[ctx.Guild.Id] = newPlaylist;

            if (notFoundTracks.Count > 0)
            {
                await ctx.Channel.SendMessageAsync($"Não foram encontradas faixas para as seguintes consultas: {string.Join(", ", notFoundTracks)}");
            }

            _interactionTokens[ctx.Guild.Id] = new CancellationTokenSource();
            _ = StartInteractivityLoop(ctx, conn, _interactionTokens[ctx.Guild.Id].Token);
        }

        private string? ExtractSpotifyPlaylistId(string url)
        {
            try
            {
                var uri = new Uri(url);
                if (uri.Host.Contains("spotify.com") && uri.AbsolutePath.StartsWith("/playlist"))
                {
                    var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length >= 2)
                        return segments[1];
                }
            }
            catch { }
            return null;
        }
    }
}
