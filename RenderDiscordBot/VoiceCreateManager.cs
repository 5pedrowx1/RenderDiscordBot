using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Google.Cloud.Firestore;

namespace DiscordBot
{
    public class VoiceCreateManager : BaseCommandModule
    {
        private readonly DiscordClient _client;
        private readonly Config _config;
        private readonly List<ulong> _createdChannels = new List<ulong>();
        private const string TempChannelsDocId = "temporary_voice_channels";
        private const string TempChannelsCollection = "bot_data";

        public VoiceCreateManager(DiscordClient client, Config config)
        {
            _client = client;
            _config = config;
            _ = LoadTemporaryChannelsAsync();
            _client.VoiceStateUpdated += OnVoiceStateUpdatedAsync;
        }

        private async Task LoadTemporaryChannelsAsync()
        {
            try
            {
                DocumentReference docRef = FirebaseService.FirestoreDb!
                    .Collection(TempChannelsCollection)
                    .Document(TempChannelsDocId);

                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
                if (snapshot.Exists)
                {
                    if (snapshot.TryGetValue<List<long>>("Channels", out var channelIds))
                    {
                        foreach (var id in channelIds)
                            _createdChannels.Add((ulong)id);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar canais temporários: {ex.Message}");
            }
        }

        private async Task SaveTemporaryChannelsAsync()
        {
            try
            {
                DocumentReference docRef = FirebaseService.FirestoreDb!
                    .Collection(TempChannelsCollection)
                    .Document(TempChannelsDocId);

                List<long> channelList = _createdChannels.Select(id => (long)id).ToList();
                var data = new Dictionary<string, object>
                {
                    { "Channels", channelList }
                };

                await docRef.SetAsync(data, SetOptions.Overwrite);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao salvar canais temporários: {ex.Message}");
            }
        }

        public async Task OnVoiceStateUpdatedAsync(DiscordClient sender, VoiceStateUpdateEventArgs e)
        {
            var afterChannelId = e.After?.Channel?.Id;

            if (!(e.User is DiscordMember member))
                return;

            var guild = e.Guild;
            if (guild == null)
                return;

            if (e.After?.Channel != null && e.After.Channel.Id == _config.VoiceCreateId)
            {
                string baseChannelName = $"👤・{member.Username}";
                string newChannelName = baseChannelName;
                int count = 1;

                while (guild.Channels.Values.Any(ch => ch.Type == ChannelType.Voice &&
                       string.Equals(ch.Name, newChannelName, StringComparison.OrdinalIgnoreCase)))
                {
                    newChannelName = $"{baseChannelName} ({count})";
                    count++;
                }

                DiscordChannel newVoiceChannel;
                if (_config.CategoryVoiceId != 0)
                {
                    var parent = guild.GetChannel(_config.CategoryVoiceId);
                    newVoiceChannel = await guild.CreateChannelAsync(newChannelName, ChannelType.Voice, parent: parent, userLimit: 10);
                }
                else
                {
                    newVoiceChannel = await guild.CreateChannelAsync(newChannelName, ChannelType.Voice, userLimit: 10);
                }

                _createdChannels.Add(newVoiceChannel.Id);
                await SaveTemporaryChannelsAsync();

                await newVoiceChannel.AddOverwriteAsync(member,
                    allow: Permissions.AccessChannels | Permissions.UseVoice | Permissions.Speak,
                    deny: Permissions.None);

                await member.PlaceInAsync(newVoiceChannel);
            }

            if (e.Before?.Channel != null && _createdChannels.Contains(e.Before.Channel.Id))
            {
                int initialCount = e.Before.Channel.Users.Count(u => !u.IsBot);

                if (initialCount == 0)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(10000);
                        var currentChannel = e.Guild.GetChannel(e.Before.Channel.Id);
                        if (currentChannel != null)
                        {
                            int finalCount = currentChannel.Users.Count(u => !u.IsBot);
                            if (finalCount == 0)
                            {
                                try
                                {
                                    await currentChannel.DeleteAsync();
                                    _createdChannels.Remove(currentChannel.Id);
                                    await SaveTemporaryChannelsAsync();
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Erro ao deletar canal {currentChannel.Id}: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            _createdChannels.Remove(e.Before.Channel.Id);
                            await SaveTemporaryChannelsAsync();
                            Console.WriteLine($"Canal já inexistente removido da lista: {e.Before.Channel.Id}");
                        }
                    });
                }
            }
        }

        public async Task CheckAndCleanTemporaryChannelsAsync()
        {
            var guild = _client.Guilds.Values.FirstOrDefault(g => g.Id == _config.ServerId);
            if (guild == null)
                return;

            var tempChannels = _createdChannels.ToList();
            var channelsToRemove = new List<ulong>();

            foreach (var channelId in tempChannels)
            {
                var channel = guild.GetChannel(channelId);
                if (channel != null && channel.Type == ChannelType.Voice)
                {
                    await Task.Delay(10000);
                    var nonBotUsers = channel.Users.Where(u => !u.IsBot).ToList();
                    if (!nonBotUsers.Any())
                    {
                        try
                        {
                            await channel.DeleteAsync();
                            channelsToRemove.Add(channelId);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao deletar canal {channelId}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    channelsToRemove.Add(channelId);
                }
            }

            foreach (var id in channelsToRemove)
            {
                _createdChannels.Remove(id);
            }
            await SaveTemporaryChannelsAsync();
        }
    }
}
