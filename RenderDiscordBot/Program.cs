using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.EventArgs;
using Google.Cloud.Firestore;
using DSharpPlus.Lavalink;
using DSharpPlus.Net;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RenderDiscordBot
{
    public sealed class Program
    {
        public static DiscordClient? Client { get; private set; }
        private static Timer? _lavalinkMonitorTimer;

        public static async Task Main(string[] args)
        {
            var webServerTask = StartWebServerAsync();
            var botTask = RunBotAsync(args);

            await Task.WhenAll(botTask, webServerTask);
        }

        private static async Task RunBotAsync(string[] args)
        {
            FirebaseService.InitializeFirebase();
            Config botConfig = await ConfigService.GetConfigFromFirestoreAsync();

            Client = new DiscordClient(new DiscordConfiguration()
            {
                Intents = DiscordIntents.All,
                Token = botConfig.Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                MinimumLogLevel = LogLevel.Debug
            });

            var lavalink = Client.UseLavalink();

            var endpoint = new ConnectionEndpoint
            {
                Hostname = "lava-all.ajieblogs.eu.org",
                Port = 443,
                Secured = true
            };

            var lavalinkConfig = new LavalinkConfiguration
            {
                Password = "https://dsc.gg/ajidevserver",
                RestEndpoint = endpoint,
                SocketEndpoint = endpoint
            };

            var commandsConfig = new CommandsNextConfiguration
            {
                StringPrefixes = new[] { botConfig.CommandPrefix },
                EnableDms = botConfig.EnableDms,
                EnableMentionPrefix = botConfig.EnableMentionPrefix,
            };

            var commands = Client.UseCommandsNext(commandsConfig);
            commands.RegisterCommands<MusicCommands.MusicCommands>();
            commands.CommandErrored += OnCommandError;

            Client.UseInteractivity(new DSharpPlus.Interactivity.InteractivityConfiguration
            {
                Timeout = TimeSpan.FromMinutes(2)
            });

            Client.Ready += OnClientReady;
            Client.MessageCreated += OnMessageCreated;

            await Client.ConnectAsync();
            await Client.GetLavalink().ConnectAsync(lavalinkConfig);

            _lavalinkMonitorTimer = new Timer(async _ => await CheckLavalinkConnection());

            await Task.Delay(-1);
        }

        private static async Task StartWebServerAsync()
        {
            var port = Environment.GetEnvironmentVariable("PORT") ?? "3000";

            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            app.MapGet("/", () => "O bot está rodando!");
            await app.RunAsync($"http://0.0.0.0:{port}");
        }

        private static async Task CheckLavalinkConnection()
        {
            try
            {
                if (Client == null) return;

                var lavalink = Client.GetLavalink();
                if (!lavalink.ConnectedNodes.Any())
                {
                    Console.WriteLine("Lavalink desconectado, tentando reconectar...");

                    var endpoint = new ConnectionEndpoint
                    {
                        Hostname = "lava-all.ajieblogs.eu.org",
                        Port = 443,
                        Secured = true
                    };

                    var lavalinkConfig = new LavalinkConfiguration
                    {
                        Password = "https://dsc.gg/ajidevserver",
                        RestEndpoint = endpoint,
                        SocketEndpoint = endpoint
                    };

                    await lavalink.ConnectAsync(lavalinkConfig);

                    if (lavalink.ConnectedNodes.Any())
                        Console.WriteLine("Reconexão com o Lavalink realizada com sucesso.");
                    else
                        Console.WriteLine("Tentativa de reconexão com o Lavalink falhou.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao verificar ou reconectar o Lavalink: {ex.Message}");
            }
        }

        private static async Task OnCommandError(CommandsNextExtension sender, CommandErrorEventArgs e)
        {
            if (e.Exception is ChecksFailedException castedException)
            {
                string cooldownTimer = string.Empty;

                foreach (var check in castedException.FailedChecks)
                {
                    var cooldown = (CooldownAttribute)check;
                    TimeSpan timeLeft = cooldown.GetRemainingCooldown(e.Context);
                    cooldownTimer = timeLeft.ToString(@"hh\:mm\:ss");
                }

                var cooldownMessage = new DiscordEmbedBuilder()
                {
                    Title = "Aguarde o término do tempo de espera",
                    Description = "Tempo restante: " + cooldownTimer,
                    Color = DiscordColor.Red
                };

                await e.Context.Channel.SendMessageAsync(embed: cooldownMessage);
            }
        }

        private static async Task OnClientReady(DiscordClient sender, ReadyEventArgs e)
        {
            Console.WriteLine("Bot está online!");
            await LogEvent("Bot iniciado com sucesso", "Ready");
        }

        private static async Task OnMessageCreated(DiscordClient sender, MessageCreateEventArgs e)
        {
            if (e.Message.Content.StartsWith("/"))
            {
                string logContent = $"Comando recebido de {e.Guild.Name}: {e.Message.Content}";
                Console.WriteLine(logContent);
                await LogEvent(logContent, "Command");
            }
        }

        private static async Task LogEvent(string message, string eventType)
        {
            try
            {
                DocumentReference docRef = FirebaseService.FirestoreDb.Collection("bot_logs").Document();
                var logData = new
                {
                    Message = message,
                    EventType = eventType,
                    Timestamp = DateTime.UtcNow
                };
                await docRef.SetAsync(logData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao gravar log: {ex.Message}");
            }
        }
    }
}