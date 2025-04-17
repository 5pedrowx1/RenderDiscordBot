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
        private string _lastStreamId;

        private const string TwitchClientId = "xbknm3de1noh1ppl94x7j183noqc77";
        private const string TwitchAccessToken = "an8q7ury7incx8czyb33fc0ce6icsc";

        public News(DiscordClient client, Config config)
        {
            _client = client;
            _config = config;
            _httpClient = new HttpClient();

            _timer = new System.Timers.Timer(30000);
            _timer.Elapsed += async (sender, e) =>
            {
                Console.WriteLine($"[{DateTime.Now}] Iniciando verificação da live...");
                await CheckLiveStatus();
            };
            _timer.Start();

            Console.WriteLine($"[{DateTime.Now}] Módulo News iniciado com timer de 30 segundos.");
        }

        private string? GetStreamerName()
        {
            if (string.IsNullOrEmpty(_config.UrlLive))
            {
                Console.WriteLine("URL da live não configurada.");
                return null;
            }

            var match = Regex.Match(_config.UrlLive, @"twitch\.tv\/([a-zA-Z0-9_]+)");
            string streamerName = match.Success ? match.Groups[1].Value : null;
            Console.WriteLine($"Streamer obtido: {streamerName}");
            return streamerName;
        }

        private async Task<string?> GetStreamerProfileImageUrl(string streamerName)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Client-ID", TwitchClientId);
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {TwitchAccessToken}");

                string url = $"https://api.twitch.tv/helix/users?login={streamerName}";
                Console.WriteLine($"Obtendo imagem de perfil do streamer através da URL: {url}");
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);
                string? profileImageUrl = json["data"]?.First?["profile_image_url"]?.ToString();
                Console.WriteLine($"Imagem de perfil obtida: {profileImageUrl}");
                return profileImageUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter a imagem de perfil do streamer: {ex.Message}");
                return null;
            }
        }

        private async Task<string?> GetStreamDescription(string streamerName)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Client-ID", TwitchClientId);
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {TwitchAccessToken}");

                string url = $"https://api.twitch.tv/helix/streams?user_login={streamerName}";
                Console.WriteLine($"Obtendo descrição da live através da URL: {url}");
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);
                string? streamDescription = json["data"]?.First?["title"]?.ToString();
                Console.WriteLine($"Descrição da live: {streamDescription}");
                return streamDescription;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter a descrição da live: {ex.Message}");
                return null;
            }
        }

        private async Task<string?> GetStreamId(string streamerName)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Client-ID", TwitchClientId);
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {TwitchAccessToken}");

                string url = $"https://api.twitch.tv/helix/streams?user_login={streamerName}";
                Console.WriteLine($"Obtendo ID da live pela URL: {url}");
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);
                string? streamId = json["data"]?.First?["id"]?.ToString();
                Console.WriteLine($"ID da live obtido: {streamId}");
                return streamId;
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
            if (string.IsNullOrEmpty(streamerName))
            {
                Console.WriteLine("Streamer não configurado.");
                return false;
            }

            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Client-ID", TwitchClientId);
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {TwitchAccessToken}");

                string url = $"https://api.twitch.tv/helix/streams?user_login={streamerName}";
                Console.WriteLine($"Verificando se o streamer está ao vivo na URL: {url}");
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);
                bool isLive = json["data"].HasValues;
                Console.WriteLine(isLive ? "Streamer está ao vivo." : "Streamer não está ao vivo.");
                return isLive;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao verificar live na Twitch: {ex.Message}");
                return false;
            }
        }

        private List<(string Text, string Type)> GetFunnyStatuses()
        {
            string streamerName = GetStreamerName();
            if (string.IsNullOrEmpty(streamerName))
            {
                return new List<(string, string)>();
            }

            return new List<(string, string)>
            {
                ($"📢 Assistindo {streamerName} ao vivo", "Streaming")
            };
        }

        public async Task SendNewsEmbed(string description, string? thumbnailUrl = null)
        {
            try
            {
                var channel = await _client.GetChannelAsync(_config.NewsChannelId);
                if (channel == null)
                {
                    Console.WriteLine($"Canal com ID {_config.NewsChannelId} não encontrado.");
                    return;
                }
                Console.WriteLine($"Canal encontrado: {channel.Name}");

                var streamerName = GetStreamerName();
                var profileImageUrl = await GetStreamerProfileImageUrl(streamerName);

                var imageUrl = thumbnailUrl ?? $"https://static-cdn.jtvnw.net/previews-ttv/live_user_{streamerName}-1920x1080.jpg";
                if (!Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
                {
                    Console.WriteLine($"URL inválida detectada: {imageUrl}");
                    imageUrl = null;
                }

                var embed = new DiscordEmbedBuilder()
                    .WithTitle($"{streamerName} está ao vivo!")
                    .WithDescription($"{description}\n\nClique no botão abaixo para assistir ao vivo.")
                    .WithColor(new DiscordColor(82, 60, 124))
                    .WithThumbnail(profileImageUrl)
                    .WithFooter("Twitch", "https://i.imgur.com/BAr5fnh.png")
                    .WithTimestamp(DateTimeOffset.UtcNow);

                if (imageUrl != null)
                    embed.WithImageUrl(imageUrl);

                var button = new DiscordLinkButtonComponent(_config.UrlLive, "🎥 Assistir ao Vivo");

                var builder = new DiscordMessageBuilder()
                    .AddEmbed(embed.Build())
                    .AddComponents(button);

                Console.WriteLine("Enviando embed com botão para o canal: " + channel.Name);
                await channel.SendMessageAsync(builder);
                Console.WriteLine("Embed enviado com sucesso.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao enviar embed: {ex.Message}");
            }
        }

        public async Task SendLiveStartNotification()
        {
            string streamerName = GetStreamerName();
            if (string.IsNullOrEmpty(streamerName))
            {
                Console.WriteLine("Streamer não configurado para notificação.");
                return;
            }

            string title = $"{streamerName} está ao vivo!";
            string? description = await GetStreamDescription(streamerName);
            if (string.IsNullOrEmpty(description))
            {
                Console.WriteLine("Descrição da live não disponível.");
                return;
            }

            string? streamId = await GetStreamId(streamerName);
            if (streamId == _lastStreamId)
            {
                Console.WriteLine("ID da live é o mesmo da última notificação. Ignorando...");
                return;
            }

            _lastStreamId = streamId;
            Console.WriteLine("Enviando notificação de live...");
            await SendNewsEmbed(title, description);
        }

        private async Task UpdateStatus()
        {
            var statuses = GetFunnyStatuses();
            if (statuses.Count == 0)
            {
                Console.WriteLine("Nenhum status divertido disponível.");
                return;
            }

            var random = new Random();
            var status = statuses[random.Next(statuses.Count)];
            var text = status.Text;
            var type = status.Type;

            if (type == "Streaming")
            {
                Console.WriteLine("Atualizando status do bot para Streaming...");
                var activity = new DiscordActivity
                {
                    Name = text,
                    ActivityType = ActivityType.Streaming,
                    StreamUrl = _config.UrlLive
                };

                await _client.UpdateStatusAsync(activity);
                Console.WriteLine("Status atualizado com sucesso.");
            }
        }

        private async Task CheckLiveStatus()
        {
            try
            {
                if (await IsStreamerLive())
                {
                    Console.WriteLine("Streamer ao vivo. Enviando notificação...");
                    await SendLiveStartNotification();
                    await UpdateStatus();
                }
                else
                {
                    Console.WriteLine("Streamer não está ao vivo. Nenhuma ação tomada.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro no CheckLiveStatus: {ex.Message}");
            }
        }
    }
}
