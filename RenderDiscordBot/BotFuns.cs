using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Google.Cloud.Firestore;

namespace RenderDiscordBot
{
    public class BotFuns : BaseCommandModule
    {
        private readonly Dictionary<ulong, int> _xpUsuarios = [];
        private readonly Random _random = new();
        private const string FirestoreCollection = "bot_data";
        private const string FirestoreDocument = "XpData";
        private readonly DiscordClient _client;
        private static Config _config = null!;

        public BotFuns(DiscordClient client, Config config)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));

            LoadXpDataAsync().GetAwaiter().GetResult();
            _client.MessageCreated += OnMessageCreated;
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

            return true;
        }

        public async Task LoadXpDataAsync()
        {
            try
            {
                DocumentReference docRef = FirebaseService.FirestoreDb!.Collection(FirestoreCollection).Document(FirestoreDocument);
                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

                if (snapshot.Exists)
                {
                    Dictionary<string, object> data = snapshot.ToDictionary();
                    if (data != null && data.ContainsKey("xpUsuarios"))
                    {
                        if (data["xpUsuarios"] is Dictionary<string, object> xpData)
                        {
                            foreach (var kvp in xpData)
                            {
                                if (ulong.TryParse(kvp.Key, out ulong userId) &&
                                    int.TryParse(kvp.Value.ToString(), out int xp))
                                {
                                    _xpUsuarios[userId] = xp;
                                }
                            }
                        }
                    }
                    else
                    {
                        await SaveXpDataAsync();
                    }
                }
                else
                {
                    await SaveXpDataAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar XP do Firestore: {ex.Message}");
            }
        }

        private async Task SaveXpDataAsync()
        {
            try
            {
                DocumentReference docRef = FirebaseService.FirestoreDb!.Collection(FirestoreCollection).Document(FirestoreDocument);
                var xpData = _xpUsuarios.ToDictionary(kvp => kvp.Key.ToString(), kvp => (object)kvp.Value);
                Dictionary<string, object> data = new()
                {
                    { "xpUsuarios", xpData }
                };
                await docRef.SetAsync(data, SetOptions.Overwrite);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao salvar XP no Firestore: {ex.Message}");
            }
        }

        private async Task OnMessageCreated(DiscordClient client, MessageCreateEventArgs e)
        {
            if (e.Author.IsBot)
                return;

            ulong userId = e.Author.Id;
            int xpGanho = 10;

            if (!_xpUsuarios.ContainsKey(userId))
                _xpUsuarios[userId] = 0;

            _xpUsuarios[userId] += xpGanho;
            await SaveXpDataAsync();
        }

        [Command("rank")]
        public async Task RankAsync(CommandContext ctx)
        {
            if (!await ValidateCommandUsage(ctx))
                return;

            var user = ctx.Message.Author;
            int xp = _xpUsuarios.ContainsKey(user.Id) ? _xpUsuarios[user.Id] : 0;
            int nivel = xp / 100;
            string thumbnailUrl = user.AvatarUrl ?? user.DefaultAvatarUrl;

            var embed = new DiscordEmbedBuilder
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor { Name = user.Username, IconUrl = thumbnailUrl },
                Title = $"🏆 Progresso do {user.Username}",
                Description = $"**Nível:** {nivel}\n**XP:** {xp}",
                Color = DiscordColor.Azure,
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail { Url = thumbnailUrl },
                Footer = new DiscordEmbedBuilder.EmbedFooter { Text = "Continue participando para ganhar mais XP!" },
                Timestamp = DateTimeOffset.Now
            };

            await ctx.Channel.SendMessageAsync(embed: embed);
        }

        [Command("ranking")]
        public async Task LeaderboardAsync(CommandContext ctx)
        {
            if (!await ValidateCommandUsage(ctx))
                return;

            if (_xpUsuarios.Count == 0)
            {
                await ctx.Channel.SendMessageAsync("Ainda não há dados de XP.");
                return;
            }

            var ranking = _xpUsuarios.OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .Select((kvp, index) =>
                {
                    var memberTask = ctx.Guild.GetMemberAsync(kvp.Key);
                    memberTask.Wait();
                    var member = memberTask.Result;
                    string username = member != null ? member.Username : "Usuário desconhecido";
                    return $"`{index + 1}.` **{username}** - {kvp.Value} XP";
                }).ToList();

            var embed = new DiscordEmbedBuilder
            {
                Title = "🏆 Ranking dos Mais Ativos",
                Description = string.Join("\n", ranking),
                Color = DiscordColor.Azure,
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail { Url = ctx.Guild.IconUrl },
                Footer = new DiscordEmbedBuilder.EmbedFooter { Text = "Continue participando para ganhar mais XP!" },
                Timestamp = DateTimeOffset.Now
            };

            await ctx.Channel.SendMessageAsync(embed: embed);
        }

        [Command("givexp")]
        public async Task GiveXpAsync(CommandContext ctx, DiscordUser user, int quantidade)
        {
            await EnsureConfigLoadedAsync();

            if (_config.AdminRoleId != 0)
            {
                var member = await ctx.Guild.GetMemberAsync(ctx.User.Id);
                if (!member.Roles.Any(role => role.Id == _config.AdminRoleId))
                {
                    var erroPermissao = new DiscordEmbedBuilder
                    {
                        Title = "❌ Permissão negada",
                        Description = "Você precisa ser um administrador para usar esse comando.",
                        Color = DiscordColor.Red
                    };
                    await ctx.Channel.SendMessageAsync(embed: erroPermissao);
                    return;
                }
            }

            if (user == null || quantidade <= 0)
            {
                var erroUso = new DiscordEmbedBuilder
                {
                    Title = "❌ Uso incorreto do comando",
                    Description = "Uso correto: `!givexp @usuário quantidade`.",
                    Color = DiscordColor.Orange
                };
                await ctx.Channel.SendMessageAsync(embed: erroUso);
                return;
            }

            if (!_xpUsuarios.ContainsKey(user.Id))
                _xpUsuarios[user.Id] = 0;

            _xpUsuarios[user.Id] += quantidade;
            await SaveXpDataAsync();

            var embed = new DiscordEmbedBuilder
            {
                Title = "✨ XP Concedido",
                Description = $"<@{user.Id}> recebeu **{quantidade} XP**!\nAgora possui **{_xpUsuarios[user.Id]} XP** no total.",
                Color = DiscordColor.Azure
            }
            .WithFooter($"Concedido por {ctx.User.Username}", ctx.User.AvatarUrl)
            .WithTimestamp(DateTimeOffset.Now);

            await ctx.Channel.SendMessageAsync(embed: embed);
        }

        [Command("caraoucoroa")]
        public async Task CoinFlipAsync(CommandContext ctx)
        {
            if (!await ValidateCommandUsage(ctx))
                return;

            var flippingEmbed = new DiscordEmbedBuilder
            {
                Title = "🪙 Jogando a moeda...",
                Description = "🔄 Girando...",
                Color = DiscordColor.Gold
            }
            .WithFooter($"Comando executado por {ctx.User.Username}", ctx.User.AvatarUrl)
            .WithTimestamp(DateTimeOffset.Now);

            var msg = await ctx.Channel.SendMessageAsync(embed: flippingEmbed);

            await Task.Delay(1500);

            string resultado = _random.Next(2) == 0 ? "Cara 🪙" : "Coroa 🪙";

            var resultEmbed = new DiscordEmbedBuilder
            {
                Title = "🪙 Resultado do Cara ou Coroa",
                Description = $"A moeda caiu em: **{resultado}**!",
                Color = DiscordColor.Azure
            }
            .WithFooter($"Comando executado por {ctx.User.Username}", ctx.User.AvatarUrl)
            .WithTimestamp(DateTimeOffset.Now);

            await msg.ModifyAsync(embed: resultEmbed.Build());
        }

        [Command("rodardado")]
        public async Task RollDiceAsync(CommandContext ctx, int lados = 6)
        {
            if (!await ValidateCommandUsage(ctx))
                return;

            if (lados < 2)
            {
                var erroEmbed = new DiscordEmbedBuilder
                {
                    Title = "❌ Dado inválido",
                    Description = "O dado deve ter pelo menos 2 lados.",
                    Color = DiscordColor.Red
                };
                await ctx.Channel.SendMessageAsync(embed: erroEmbed);
                return;
            }

            var rollingEmbed = new DiscordEmbedBuilder
            {
                Title = "🎲 Rolando o dado...",
                Description = "🔄🔄🔄",
                Color = DiscordColor.Gold
            }
            .WithFooter($"Comando executado por {ctx.User.Username}", ctx.User.AvatarUrl)
            .WithTimestamp(DateTimeOffset.Now);

            var msg = await ctx.Channel.SendMessageAsync(embed: rollingEmbed);

            await Task.Delay(1500);

            int resultado = _random.Next(1, lados + 1);

            var finalEmbed = new DiscordEmbedBuilder
            {
                Title = "🎲 Resultado da Rolagem",
                Description = $"Você rolou um dado de **{lados}** lados e tirou **{resultado}**!",
                Color = DiscordColor.Azure
            }
            .WithFooter($"Comando executado por {ctx.User.Username}", ctx.User.AvatarUrl)
            .WithTimestamp(DateTimeOffset.Now);

            await msg.ModifyAsync(embed: finalEmbed.Build());
        }

        [Command("motivação")]
        public async Task QuoteAsync(CommandContext ctx)
        {
            if (!await ValidateCommandUsage(ctx))
                return;

            string[] quotes =
            [
                "A persistência realiza o impossível. 💪",
                "O sucesso nasce do esforço contínuo. 🚀",
                "Acredite no seu potencial! 🌟",
                "Nunca é tarde para recomeçar. ⏳",
                "Seja a mudança que você quer ver no mundo. ✨",
                "Grandes conquistas começam com pequenos passos. 👣",
                "Você é mais forte do que imagina. 🧠🔥",
                "Desafios são oportunidades disfarçadas. 🎯",
                "A disciplina é o combustível do sucesso. ⛽",
                "Foco, força e fé te levam além. 🛤️",
                "Não pare até se orgulhar. 🏁",
                "A jornada é tão importante quanto o destino. 🗺️",
                "Crescimento começa fora da zona de conforto. 🧗",
                "Todo esforço vale a pena quando a alma não é pequena. 🧡",
                "A ação é a chave fundamental para todo sucesso. 🔑"
            ];

            string quoteAleatoria = quotes[_random.Next(quotes.Length)];

            var embed = new DiscordEmbedBuilder
            {
                Title = "📢 Citação Motivacional",
                Description = quoteAleatoria,
                Color = new DiscordColor("#00bfff"),
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"Enviado por {ctx.User.Username}",
                    IconUrl = ctx.User.AvatarUrl
                },
                Timestamp = DateTimeOffset.Now
            };

            await ctx.Channel.SendMessageAsync(embed: embed);
        }
    }
}
