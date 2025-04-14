using DSharpPlus.Entities;
using DSharpPlus.CommandsNext;
using DSharpPlus;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Text.RegularExpressions;
/*
.
namespace RenderDiscordBot
{
    class News : BaseCommandModule
    {
        private readonly DiscordClient _client;
        private readonly Config _config;
        private readonly HttpClient _httpClient;
        private readonly System.Timers.Timer _timer;
        private string _lastStreamId;

        private const string TwitchClientId = "xbknm3de1noh1ppl94x7j183noqc77";
        private const string TwitchAccessToken = "vadlb5jy6xptyfjzkeof29uumr1i3w";

        public News(DiscordClient client, Config config)
        {
            _client = client;
            _config = config;
            _httpClient = new HttpClient();
            _timer = new System.Timers.Timer(60000);
            _timer.Elapsed += async (sender, e) => await CheckLiveStatus();
            _timer.Start();
        }

        private string GetStreamerName()
        {
            if (string.IsNullOrEmpty(_config.UrlLive))
                return null;

            var match = Regex.Match(_config.UrlLive, @"twitch\.tv\/([a-zA-Z0-9_]+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        private async Task<string> GetStreamerProfileImageUrl(string streamerName)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Client-ID", TwitchClientId);
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {TwitchAccessToken}");

                string url = $"https://api.twitch.tv/helix/users?login={streamerName}";
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                return json["data"]?.First?["profile_image_url"]?.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter a imagem de perfil do streamer: {ex.Message}");
                return null;
            }
        }

        private async Task<string> GetStreamDescription(string streamerName)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Client-ID", TwitchClientId);
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {TwitchAccessToken}");

                string url = $"https://api.twitch.tv/helix/streams?user_login={streamerName}";
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                return json["data"]?.First?["title"]?.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter a descrição da live: {ex.Message}");
                return null;
            }
        }

        private List<(string Text, string Type)> GetFunnyStatuses()
        {
            string streamerName = GetStreamerName();
            if (string.IsNullOrEmpty(streamerName)) return new List<(string, string)>();

            return new List<(string, string)>
            {
                ($"📢 Assistindo {streamerName} ao vivo", "Streaming")
            };
        }

        public async Task SendNewsEmbed(string description, string thumbnailUrl = null)
        {
            if (!(_client.GetChannel(_config.NewsChannelId) is SocketTextChannel channel))
            {
                Logs.Error($"Canal com ID {_config.NewsChannelId} não encontrado.");
                return;
            }

            var streamerName = GetStreamerName();
            var profileImageUrl = await GetStreamerProfileImageUrl(streamerName);
            string UrlImage = "https://i.imgur.com/BAr5fnh.png";
            var embed = new EmbedBuilder()
                .WithTitle(streamerName)
                .WithDescription($"{description}\n\n")
                .WithColor(new Discord.Color(82, 60, 124))
                .WithThumbnailUrl(profileImageUrl)
                .WithImageUrl(thumbnailUrl ?? $"https://static-cdn.jtvnw.net/previews-ttv/live_user_{streamerName}-1920x1080.jpg")
                .WithFooter("Twitch", UrlImage)
                .WithCurrentTimestamp()
                .Build();

            var components = new ComponentBuilder()
                .WithButton("Assistir ao Vivo", null, ButtonStyle.Link, url: $"{_config.UrlLive}");

            await channel.SendMessageAsync(embed: embed, components: components.Build());
        }

        public async Task SendLiveStartNotification()
        {
            string streamerName = GetStreamerName();
            if (string.IsNullOrEmpty(streamerName)) return;

            string title = $"{streamerName} está ao vivo!";
            string description = await GetStreamDescription(streamerName);
            if (string.IsNullOrEmpty(description)) return;

            string streamId = await GetStreamId(streamerName);
            if (streamId == _lastStreamId) return;

            _lastStreamId = streamId;

            await SendNewsEmbed(title, description);
        }

        private async Task<string> GetStreamId(string streamerName)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Client-ID", TwitchClientId);
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {TwitchAccessToken}");

                string url = $"https://api.twitch.tv/helix/streams?user_login={streamerName}";
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                return json["data"]?.First?["id"]?.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter o ID da live: {ex.Message}");
                return null;
            }
        }

        private async Task<bool> IsStreamerLive()
        {
            string streamerName = GetStreamerName();
            if (string.IsNullOrEmpty(streamerName)) return false;

            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Client-ID", TwitchClientId);
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {TwitchAccessToken}");

                string url = $"https://api.twitch.tv/helix/streams?user_login={streamerName}";
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                return json["data"].HasValues;
            }
            catch (Exception ex)
            {
                Logs.Error($"Erro ao verificar live na Twitch: {ex.Message}");
                return false;
            }
        }

        private async Task UpdateStatus()
        {
            if (_client.ConnectionState == ConnectionState.Connected)
            {
                var statuses = GetFunnyStatuses();
                if (statuses.Count == 0) return;

                var status = statuses[new Random().Next(statuses.Count)];
                var text = status.Text;
                var type = status.Type;

                if (type == "Streaming")
                {
                    await _client.SetGameAsync(text, _config.UrlLive, ActivityType.Streaming);
                }
            }
        }

        private async Task CheckLiveStatus()
        {
            if (await IsStreamerLive())
            {
                await SendLiveStartNotification();
                await UpdateStatus();
            }
        }
    }
}
*/
