using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace RenderDiscordBot
{
    public class TicketHandler : BaseCommandModule
    {
        private readonly DiscordClient _client;
        private readonly Config _config;

        public TicketHandler(DiscordClient client, Config config)
        {
            _client = client;
            _config = config;
            _client.ComponentInteractionCreated += OnComponentInteractionCreated;
        }

        private DiscordChannel GetTicketCategory(DiscordGuild guild)
        {
            var category = guild.Channels.Values.FirstOrDefault(c => c.Type == ChannelType.Category && c.Id == _config.CategorySuportId);
            return category ?? throw new Exception("Categoria de suporte não encontrada.");
        }

        private async Task OnComponentInteractionCreated(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            var interaction = e.Interaction;

            if (interaction.Data.CustomId == "open_ticket" && interaction.User is DiscordMember user)
            {
                var guild = e.Guild;
                var category = GetTicketCategory(guild);

                if (category != null)
                {
                    var ticketChannel = await guild.CreateChannelAsync($"Ticket-{user.Username}", ChannelType.Text, parent: category);

                    if (ticketChannel != null)
                    {
                        await ticketChannel.AddOverwriteAsync(guild.EveryoneRole,
                            allow: Permissions.None,
                            deny: Permissions.AccessChannels);

                        await ticketChannel.AddOverwriteAsync(user,
                            allow: Permissions.AccessChannels | Permissions.SendMessages,
                            deny: Permissions.None);

                        var supportRole = guild.GetRole(_config.AdminRoleId);
                        if (supportRole != null)
                        {
                            await ticketChannel.AddOverwriteAsync(supportRole,
                                allow: Permissions.AccessChannels | Permissions.SendMessages,
                                deny: Permissions.None);
                        }

                        var embed = new DiscordEmbedBuilder
                        {
                            Title = "🎟️ Ticket de Suporte Aberto",
                            Description = $"Olá {user.Mention}! 👋\n\nUm membro da nossa equipe de suporte irá te atender em breve.\n\n🔒 **Apenas um Administrador pode fechar este ticket.**",
                            Color = DiscordColor.Azure,
                            Timestamp = DateTime.UtcNow
                        };

                        if (guild.IconUrl != null)
                            embed.Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail { Url = guild.IconUrl };

                        embed.WithFooter($"Ticket aberto por {user.Username}", user.AvatarUrl ?? user.DefaultAvatarUrl);

                        var closeButton = new DiscordButtonComponent(ButtonStyle.Danger, "close_ticket", "🔒 Fechar Ticket");

                        var messageBuilder = new DiscordMessageBuilder()
                            .AddEmbed(embed)
                            .AddComponents(closeButton);

                        await ticketChannel.SendMessageAsync(messageBuilder);

                        await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder()
                                .WithContent($"✅ Seu ticket foi criado com sucesso! {ticketChannel.Mention}")
                                .AsEphemeral(true));
                    }
                    else
                    {
                        await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder()
                                .WithContent("❌ Erro: Não conseguimos criar o canal de ticket.")
                                .AsEphemeral(true));
                    }
                }
                else
                {
                    await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ Erro: Não conseguimos encontrar a categoria de tickets.")
                            .AsEphemeral(true));
                }
            }
            else if (interaction.Data.CustomId == "close_ticket")
            {
                if (interaction.User is DiscordMember guildUser &&
                    guildUser.Roles.Any(r => r.Id == _config.AdminRoleId))
                {
                    var ticketChannel = e.Channel;
                    if (ticketChannel != null)
                    {
                        await ticketChannel.SendMessageAsync("🔒 O ticket está sendo fechado...");
                        await Task.Delay(1000);
                        await ticketChannel.DeleteAsync();
                    }
                    else
                    {
                        await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder()
                                .WithContent("❌ Erro: O canal foi apagado ou não pode ser encontrado para fechamento.")
                                .AsEphemeral(true));
                    }
                }
                else
                {
                    await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ Apenas a equipe de suporte pode fechar tickets.")
                            .AsEphemeral(true));
                }
            }
        }

        [Command("Suportembed")]
        public async Task EmbedCommand(CommandContext ctx)
        {
            await ctx.Message.DeleteAsync();

            if (_config.AdminRoleId != 0)
            {
                var member = await ctx.Guild.GetMemberAsync(ctx.User.Id);
                if (!member.Roles.Any(role => role.Id == _config.AdminRoleId))
                {
                    await ctx.RespondAsync("❌ Você precisa ser um administrador para usar esse comando.");
                    return;
                }
            }

            var embed = new DiscordEmbedBuilder
            {
                Title = "📩 Suporte Personalizado",
                Description = "Se você precisa de ajuda, clique no botão abaixo para abrir um ticket com a nossa equipe de suporte.\n\n🔒 **Seu ticket será visível apenas para você e a staff.**",
                Color = DiscordColor.Azure,
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                {
                    Url = ctx.Guild.IconUrl
                },
                Timestamp = DateTime.UtcNow
            };

            embed.AddField("📌 Quando abrir um ticket?",
                "- Problemas técnicos\n- Denúncias\n- Dúvidas gerais\n- Sugestões");

            embed.WithFooter($"Comando executado por {ctx.User.Username}", ctx.User.AvatarUrl ?? ctx.User.DefaultAvatarUrl);

            var button = new DiscordButtonComponent(ButtonStyle.Primary, "open_ticket", "🔓 Abrir Ticket");

            var messageBuilder = new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddComponents(button);

            await ctx.Channel.SendMessageAsync(messageBuilder);
        }

        public async Task<bool> BlockChannel(DiscordMessage message)
        {
            if (message.Channel.Id != _config.ComandsChannelId)
            {
                Console.WriteLine($"Comando bloqueado! Canal incorreto: {message.Channel.Name} (ID: {message.Channel.Id})");

                var embed = new DiscordEmbedBuilder
                {
                    Title = "Canal de Comandos Inválido",
                    Description = "Por favor, utilize o canal de comandos designado para interagir com o bot.",
                    Color = DiscordColor.Red
                };

                await message.Channel.SendMessageAsync(embed: embed);
                return true;
            }
            return false;
        }
    }
}
