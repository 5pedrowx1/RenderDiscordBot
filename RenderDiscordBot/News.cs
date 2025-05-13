using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
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
                var match = Regex.Match(url, @"twitch\.tv\/([a-zA-Z0-9_]+)");
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

        private async Task SendLiveNotification(string streamer)
        {
            var channel = await _client.GetChannelAsync(_config.NewsChannelId);
            if (channel == null)
                return;

            var title = $"{streamer} está ao vivo!";
            var description = await GetStreamField(streamer, "title");
            var streamId = await GetStreamField(streamer, "id");

            // Evita notificações duplicadas
            if (_lastStreamIds.TryGetValue(streamer, out var lastId) && lastId == streamId)
                return;

            _lastStreamIds[streamer] = streamId;

            // Pega imagem de perfil
            var profileJson = await TwitchApiGetAsync($"users?login={streamer}");
            var profileImage = profileJson?["data"]?.First?["profile_image_url"]?.ToString();
            var previewImage = $"https://static-cdn.jtvnw.net/previews-ttv/live_user_{streamer}-1920x1080.jpg";

            var embed = new DiscordEmbedBuilder()
                .WithTitle(title)
                .WithDescription(description ?? string.Empty)
                .WithThumbnail(profileImage)
                .WithImageUrl(previewImage)
                .WithColor(new DiscordColor(82, 60, 124))
                .WithFooter("Twitch", "https://i.imgur.com/BAr5fnh.png")
                .WithTimestamp(DateTimeOffset.UtcNow);

            var button = new DiscordLinkButtonComponent($"https://twitch.tv/{streamer}", "🎥 Assistir ao Vivo");
            var message = new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddComponents(button);

            await channel.SendMessageAsync(message);
            Console.WriteLine($"Notificação enviada para {streamer}.");
        }

        private async Task CheckAllStreams()
        {
            foreach (var streamer in GetStreamerNames())
            {
                try
                {
                    if (await IsStreamerLive(streamer))
                    {
                        Console.WriteLine($"{streamer} está online.");
                        await SendLiveNotification(streamer);
                    }
                    else
                    {
                        Console.WriteLine($"{streamer} OFFLINE.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao processar {streamer}: {ex.Message}");
                }
            }
        }
    }
}
