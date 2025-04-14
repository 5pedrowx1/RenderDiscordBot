using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Google.Cloud.Firestore;

namespace RenderDiscordBot
{
    public class MiniRPG : BaseCommandModule
    {
        private const string UsersCollection = "rpg_users";
        private readonly DiscordClient _client;
        private static Config _config = null!;

        public MiniRPG(DiscordClient client, Config config)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _client.ComponentInteractionCreated += HandleComponentInteraction;
        }

        private static async Task EnsureConfigLoadedAsync()
        {
            _config ??= await ConfigService.GetConfigFromFirestoreAsync();
        }

        private async Task<bool> ValidateCommandUsage(CommandContext ctx)
        {
            await EnsureConfigLoadedAsync();

            if (ctx.Channel.Id != _config.MiniRPGChannelId)
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

        [Command("rpg")]
        public async Task OpenRPG(CommandContext ctx)
        {
            if (!await ValidateCommandUsage(ctx))
                return;

            var embed = new DiscordEmbedBuilder
            {
                Title = "🏰 Mini RPG",
                Description = "Bem-vindo ao Mini RPG!\nSelecione uma opção abaixo:",
                Color = DiscordColor.Gold,
                Timestamp = DateTimeOffset.Now
            }.Build();

            var messageBuilder = new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddComponents(
                    new DiscordButtonComponent(ButtonStyle.Primary, "rpg_profile", "Ver Perfil"),
                    new DiscordButtonComponent(ButtonStyle.Primary, "rpg_store", "Loja"),
                    new DiscordButtonComponent(ButtonStyle.Primary, "rpg_adventure", "Aventura")
                );

            await ctx.Channel.SendMessageAsync(messageBuilder);
        }

        private async Task EnsureUserProfileAsync(ulong userId)
        {
            DocumentReference docRef = FirebaseService.FirestoreDb!.Collection(UsersCollection).Document(userId.ToString());
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
            if (!snapshot.Exists)
            {
                var initialData = new Dictionary<string, object>
                {
                    { "level", 1 },
                    { "xp", 0 },
                    { "gold", 100 },
                    { "inventory", new List<string>() }
                };
                await docRef.SetAsync(initialData);
            }
        }

        [Command("perfil")]
        public async Task ProfileAsync(CommandContext ctx)
        {
            if (!await ValidateCommandUsage(ctx))
                return;

            ulong userId = ctx.User.Id;
            await EnsureUserProfileAsync(userId);

            DocumentReference docRef = FirebaseService.FirestoreDb!.Collection(UsersCollection).Document(userId.ToString());
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            int level = snapshot.GetValue<int>("level");
            int xp = snapshot.GetValue<int>("xp");
            int gold = snapshot.GetValue<int>("gold");
            var inventory = snapshot.GetValue<List<string>>("inventory");

            var embed = new DiscordEmbedBuilder
            {
                Title = "🔍 Seu Perfil RPG",
                Description = $"**Level:** {level}\n**XP:** {xp}\n**Ouro:** {gold}",
                Color = DiscordColor.Azure,
                Timestamp = DateTimeOffset.Now
            };

            if (inventory.Count > 0)
            {
                embed.AddField("Inventário", string.Join(", ", inventory));
            }
            else
            {
                embed.AddField("Inventário", "Vazio");
            }

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }

        [Command("loja")]
        public async Task StoreAsync(CommandContext ctx)
        {
            if (!await ValidateCommandUsage(ctx))
                return;

            var storeItems = new Dictionary<string, int>
            {
                { "Espada", 150 },
                { "Escudo", 100 },
                { "Poção de Vida", 50 }
            };

            var embed = new DiscordEmbedBuilder
            {
                Title = "🏪 Loja do Mini RPG",
                Description = "Selecione um item para comprar:",
                Color = DiscordColor.Gold,
                Timestamp = DateTimeOffset.Now
            };

            foreach (var item in storeItems)
            {
                embed.AddField(item.Key, $"Preço: {item.Value} ouro", inline: true);
            }

            var messageBuilder = new DiscordMessageBuilder()
                .AddEmbed(embed.Build());
            foreach (var item in storeItems)
            {
                messageBuilder.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, $"buy_{item.Key}", $"Comprar {item.Key}"));
            }

            await ctx.Channel.SendMessageAsync(messageBuilder);
        }

        public async Task HandleComponentInteraction(DiscordClient sender, ComponentInteractionCreateEventArgs e)
        {
            if (e.Interaction.Data.CustomId.StartsWith("rpg_"))
            {
                switch (e.Interaction.Data.CustomId)
                {
                    case "rpg_profile":
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder()
                                .WithContent("Use o comando `!perfil` para ver seu perfil."));
                        break;
                    case "rpg_store":
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder()
                                .WithContent("Use o comando `!loja` para acessar a loja."));
                        break;
                    case "rpg_adventure":
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder()
                                .WithContent("Em breve: sistema de aventura!"));
                        break;
                }
            }
            else if (e.Interaction.Data.CustomId.StartsWith("buy_"))
            {
                string itemName = e.Interaction.Data.CustomId.Substring(4);
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Você comprou **{itemName}**! (Lógica de compra aqui...)"));
            }
        }
    }
}
