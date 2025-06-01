using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace RenderDiscordBot
{
    public class News : BaseCommandModule
    {
        private readonly DiscordClient _client;
        private readonly Config _config;
        private readonly HttpClient _httpClient;
        private readonly System.Timers.Timer _timer;
        private readonly Dictionary<string, string?> _lastStreamIds = [];
        private static readonly Regex StreamerRegex = new(@"twitch\.tv\/([a-zA-Z0-9_]+)", RegexOptions.Compiled);
        private readonly ulong MileneServerID = 760314806493249587;
        private readonly ulong MileneNewsChannel = 1167112631623098398;
        private const string TwitchClientId = "xbknm3de1noh1ppl94x7j183noqc77";
        private const string TwitchAccessToken = "an8q7ury7incx8czyb33fc0ce6icsc";

        public News(DiscordClient client, Config config)
        {
            _client = client;
            _config = config;
            _httpClient = new HttpClient();

            _timer = new System.Timers.Timer(30000);
            _timer.Elapsed += async (_, __) => await CheckAllStreams();
            _timer.Start();
            Console.WriteLine($"[{DateTime.Now}] Módulo News iniciado (checagem a cada 30s).");
        }

        private IEnumerable<string> GetStreamerNames()
        {
            foreach (var url in _config.UrlLives)
            {
                var match = StreamerRegex.Match(url);
                if (match.Success)
                    yield return match.Groups[1].Value;
                else
                    Console.WriteLine($"URL inválida: {url}");
            }
        }

        private async Task<JObject?> TwitchApiGetAsync(string endpoint)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Client-ID", TwitchClientId);
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {TwitchAccessToken}");

                var jsonString = await _httpClient.GetStringAsync($"https://api.twitch.tv/helix/{endpoint}");
                return JObject.Parse(jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro Twitch API ({endpoint}): {ex.Message}");
                return null;
            }
        }

        private async Task<bool> IsStreamerLive(string streamer)
        {
            var json = await TwitchApiGetAsync($"streams?user_login={streamer}");
            return json?["data"]?.HasValues == true;
        }

        private async Task<string?> GetStreamField(string streamer, string field)
        {
            var json = await TwitchApiGetAsync($"streams?user_login={streamer}");
            return json?["data"]?.First?[field]?.ToString();
        }

        private async Task SendLiveNotification(string streamer, DiscordChannel channel)
        {
            var title = $"{streamer} está ao vivo!";
            var description = await GetStreamField(streamer, "title");
            var streamId = await GetStreamField(streamer, "id");

            if (_lastStreamIds.TryGetValue(streamer, out var last) && last == streamId)
                return;
            _lastStreamIds[streamer] = streamId;

            var profileJson = await TwitchApiGetAsync($"users?login={streamer}");
            var profileImg = profileJson?["data"]?.First?["profile_image_url"]?.ToString();
            var previewImg = $"https://static-cdn.jtvnw.net/previews-ttv/live_user_{streamer}-1920x1080.jpg";

            var embed = new DiscordEmbedBuilder()
                .WithTitle(title)
                .WithDescription(description ?? string.Empty)
                .WithThumbnail(profileImg)
                .WithImageUrl(previewImg)
                .WithColor(new DiscordColor(82, 60, 124))
                .WithFooter("Twitch", "https://i.imgur.com/BAr5fnh.png")
                .WithTimestamp(DateTimeOffset.UtcNow);

            var brokenUrl = $"https://twitch.tv\u200B/{streamer}";
            var button = new DiscordLinkButtonComponent(
                brokenUrl,
                "🎥 Assistir ao Vivo"
            );

            var builder = new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddComponents(button);

            await channel.SendMessageAsync(builder);
            Console.WriteLine($"Notificação de {streamer} enviada para canal {channel.Name}.");
        }


        private async Task CheckAllStreams()
        {
            var streamers = GetStreamerNames().ToList();

            for (int i = 0; i < streamers.Count; i++)
            {
                var streamer = streamers[i];

                try
                {
                    if (!await IsStreamerLive(streamer))
                    {
                        Console.WriteLine($"{streamer} OFFLINE.");
                        continue;
                    }

                    if (i == 0)
                    {
                        var testChannel = await _client.GetChannelAsync(_config.NewsChannelId);
                        if (testChannel.GuildId == MileneServerID)
                        {
                            var target = await _client.GetChannelAsync(MileneNewsChannel);
                            if (target != null)
                                await SendLiveNotification(streamer, target);
                            else
                                Console.WriteLine("Canal MileneNewsChannel não encontrado.");
                        }
                        else
                        {
                            Console.WriteLine($"Ignorada live de {streamer}: não estamos no servidor {MileneServerID}.");
                        }

                        continue;
                    }

                    var defaultChannel = await _client.GetChannelAsync(_config.NewsChannelId);
                    if (defaultChannel != null)
                        await SendLiveNotification(streamer, defaultChannel);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao processar {streamer}: {ex.Message}");
                }
            }
        }
    }
}
