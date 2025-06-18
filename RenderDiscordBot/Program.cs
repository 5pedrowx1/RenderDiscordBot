using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.Lavalink;
using DSharpPlus.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RenderDiscordBot
{
    public sealed class Program
    {
        public static DiscordClient? Client { get; private set; }
        private static Config? BotConfig;
        private static Timer? _lavalinkMonitorTimer;

        static async Task Main()
        {
            string firebaseEncryptionKey = Environment.GetEnvironmentVariable("FIREBASE_ENCRYPTION_KEY")
                ?? throw new Exception("Chave de criptografia para o Firebase não configurada.");
            FirebaseService.InitializeFirebase(firebaseEncryptionKey);
            BotConfig = await ConfigService.GetConfigFromFirestoreAsync();

            Client = new DiscordClient(new DiscordConfiguration()
            {
                Intents = DiscordIntents.All,
                Token = BotConfig.Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                MinimumLogLevel = LogLevel.None
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
                SocketEndpoint = endpoint,
                SocketAutoReconnect = true
            };

            var services = new ServiceCollection();
            services.AddSingleton(Client);
            services.AddSingleton(BotConfig);
            services.AddSingleton<EntryHandler>();
            services.AddSingleton<TicketHandler>();
            services.AddSingleton<SuggestionsHandler>();
            services.AddSingleton<SuggestionService>();
            services.AddSingleton<VoiceCreateManager>();
            services.AddSingleton<BotFuns>();
            services.AddSingleton<AdmCommands>();
            services.AddSingleton<News>();
            var serviceProvider = services.BuildServiceProvider();
            var entryHandler = serviceProvider.GetRequiredService<EntryHandler>();
            Client.GuildMemberAdded += entryHandler.OnUserJoinedAsync;
            Client.GuildMemberUpdated += entryHandler.OnUserUpdatedAsync;
            var newsModule = serviceProvider.GetRequiredService<News>();

            var commandsConfig = new CommandsNextConfiguration
            {
                StringPrefixes = [BotConfig.CommandPrefix],
                EnableDms = BotConfig.EnableDms,
                EnableMentionPrefix = BotConfig.EnableMentionPrefix,
                Services = serviceProvider
            };

            var commands = Client.UseCommandsNext(commandsConfig);
            commands.RegisterCommands<TicketHandler>();
            commands.RegisterCommands<SuggestionsHandler>();
            commands.RegisterCommands<MusicCommands>();
            commands.RegisterCommands<VoiceCreateManager>();
            commands.RegisterCommands<BotFuns>();
            commands.RegisterCommands<AdmCommands>();
            commands.CommandErrored += OnCommandError;

            Client.UseInteractivity(new DSharpPlus.Interactivity.InteractivityConfiguration
            {
                Timeout = TimeSpan.FromMinutes(2)
            });

            await Client.ConnectAsync();
            await Client.GetLavalink().ConnectAsync(lavalinkConfig);
            _lavalinkMonitorTimer = new Timer(async _ => await CheckLavalinkConnection(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            var httpServer = new HttpServer();
            _ = httpServer.StartAsync();
            Console.WriteLine("Bot está online!");
            await Task.Delay(-1);
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
                    if (check is CooldownAttribute cooldown)
                    {
                        TimeSpan timeLeft = cooldown.GetRemainingCooldown(e.Context);
                        cooldownTimer = timeLeft.ToString(@"hh\:mm\:ss");
                    }
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
    }
}
