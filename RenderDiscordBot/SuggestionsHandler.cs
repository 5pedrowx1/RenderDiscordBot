using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity.Extensions;

namespace DiscordBot
{
    public class SuggestionsHandler : BaseCommandModule
    {
        private readonly SuggestionService _service;
        private readonly DiscordClient _client;
        private readonly Config _config;

        public SuggestionsHandler(DiscordClient client, SuggestionService service, Config config)
        {
            _client = client;
            _service = service;
            _config = config;
            _client.ComponentInteractionCreated += OnComponentInteraction;
        }

        [Command("sugerir")]
        public async Task Sugerir(CommandContext ctx)
        {
            await SendSuggestionPrompt(ctx.Channel, ctx.User.Id);
        }

        private async Task SendSuggestionPrompt(DiscordChannel channel, ulong userId)
        {
            var button = new DiscordButtonComponent(
                ButtonStyle.Primary,
                $"abrir_formulario_{userId}",
                "✍️ Enviar Sugestão"
            );

            var embed = new DiscordEmbedBuilder()
                .WithTitle("Envie sua sugestão")
                .WithDescription("Clique no botão abaixo para preencher sua sugestão em uma mensagem privada.")
                .WithColor(DiscordColor.Blurple)
                .WithThumbnail(channel.Guild.IconUrl)
                .AddField("Como funciona",
                          "1. Clique no botão abaixo\n" +
                          "2. Envie sua sugestão via DM\n" +
                          "3. Aguarde aprovação", false)
                .AddField("Dicas",
                          "• Seja objetivo (máx. 200 caracteres)\n" +
                          "• Cite exemplos concretos\n" +
                          "• Use linguagem respeitosa", false)
                .WithFooter($"Servidor: {channel.Guild.Name}", channel.Guild.IconUrl)
                .WithTimestamp(DateTime.UtcNow);

            await channel.SendMessageAsync(new DiscordMessageBuilder()
                .WithEmbed(embed)
                .AddComponents(button));
        }

        private async Task OnComponentInteraction(DiscordClient sender, ComponentInteractionCreateEventArgs e)
        {
            var id = e.Id;

            if (id.StartsWith("abrir_formulario_"))
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("📬 Verifique sua DM para continuar.")
                        .AsEphemeral(true));

                var member = await e.Guild.GetMemberAsync(e.User.Id);
                var dm = await member.CreateDmChannelAsync();

                await dm.SendMessageAsync("✍️ Envie agora a sua sugestão nesta mensagem.");

                var interactivity = _client.GetInteractivity();
                var response = await interactivity.WaitForMessageAsync(
                    m => m.Channel.Id == dm.Id && m.Author.Id == e.User.Id,
                    TimeSpan.FromMinutes(2)
                );

                if (response.TimedOut)
                {
                    await dm.SendMessageAsync("⏰ Tempo esgotado. Tente novamente.");
                    return;
                }

                await HandleSuggestionSubmission(e.User, response.Result.Content);
                await dm.SendMessageAsync("✅ Sua sugestão foi enviada com sucesso!");
            }
            else if (id.StartsWith("like_") || id.StartsWith("dislike_"))
            {
                var parts = id.Split('_');
                if (parts.Length != 2 || !int.TryParse(parts[1], out var suggestionId))
                    return;

                var suggestion = _service.GetSuggestion(suggestionId);
                if (suggestion == null)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ Sugestão não encontrada.")
                            .AsEphemeral(true));
                    return;
                }

                var voterId = e.User.Id;
                bool éLike = id.StartsWith("like_");

                if (éLike && suggestion.LikedBy.Contains(voterId))
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❗ Você já curtiu esta sugestão.")
                            .AsEphemeral(true));
                    return;
                }
                if (!éLike && suggestion.DislikedBy.Contains(voterId))
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❗ Você já desaprovou esta sugestão.")
                            .AsEphemeral(true));
                    return;
                }

                if (éLike && suggestion.DislikedBy.Contains(voterId))
                    suggestion.DislikedBy.Remove(voterId);
                if (!éLike && suggestion.LikedBy.Contains(voterId))
                    suggestion.LikedBy.Remove(voterId);

                if (éLike)
                    suggestion.LikedBy.Add(voterId);
                else
                    suggestion.DislikedBy.Add(voterId);

                if (suggestion.ChannelId == null || suggestion.MessageId == null)
                    return;

                var guild = _client.Guilds[_config.ServerId];
                if (!guild.Channels.TryGetValue(suggestion.ChannelId.Value, out var channel))
                    return;

                var message = await channel.GetMessageAsync(suggestion.MessageId.Value);

                var updatedEmbed = new DiscordEmbedBuilder()
                    .WithTitle($"📬 Sugestão #{suggestion.Id}")
                    .WithDescription(suggestion.Content)
                    .WithFooter($"Por {e.User.Username}#{e.User.Discriminator}", e.User.AvatarUrl)
                    .WithTimestamp(suggestion.Timestamp)
                    .WithColor(DiscordColor.Azure)
                    .AddField("👍 Likes", suggestion.Likes.ToString(), true)
                    .AddField("👎 Dislikes", suggestion.Dislikes.ToString(), true);

                var likeButton = new DiscordButtonComponent(
                    ButtonStyle.Success,
                    $"like_{suggestion.Id}",
                    $"👍 {suggestion.Likes}"
                );
                var dislikeButton = new DiscordButtonComponent(
                    ButtonStyle.Danger,
                    $"dislike_{suggestion.Id}",
                    $"👎 {suggestion.Dislikes}"
                );
                var debateButton = new DiscordButtonComponent(
                    ButtonStyle.Secondary,
                    $"debate_{suggestion.Id}",
                    "📢 Debater com staff"
                );

                var builder = new DiscordMessageBuilder()
                    .WithEmbed(updatedEmbed)
                    .AddComponents([likeButton, dislikeButton, debateButton]);

                await message.ModifyAsync(builder);

                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
            }
            else if (id.StartsWith("debate_"))
            {
                var parts = id.Split('_');
                if (parts.Length != 2 || !int.TryParse(parts[1], out var suggestionId))
                    return;

                var suggestion = _service.GetSuggestion(suggestionId);
                if (suggestion == null)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ Sugestão não encontrada.")
                            .AsEphemeral(true));
                    return;
                }

                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("🔒 Canal fechado para debate privado com a staff.")
                        .AsEphemeral(true));

                if (suggestion.ChannelId == null)
                    return;

                var guild = _client.Guilds[_config.ServerId];
                if (!guild.Channels.TryGetValue(suggestion.ChannelId.Value, out var channel))
                    return;

                var everyoneRole = guild.GetRole(_config.MemberRoleId);
                var admRole = guild.GetRole(_config.AdminRoleId)!;
                var modRole = guild.GetRole(_config.ModRoleId)!;
                var authorMember = await guild.GetMemberAsync(suggestion.AuthorId);

                await channel.AddOverwriteAsync(
                    everyoneRole,
                    Permissions.None,
                    Permissions.AccessChannels
                );

                await channel.AddOverwriteAsync(
                    modRole,
                    Permissions.AccessChannels | Permissions.SendMessages | Permissions.ReadMessageHistory,
                    Permissions.None
                );

                await channel.AddOverwriteAsync(
                    admRole,
                    Permissions.AccessChannels | Permissions.SendMessages | Permissions.ReadMessageHistory,
                    Permissions.None
                );

                await channel.AddOverwriteAsync(
                    authorMember,
                    Permissions.AccessChannels | Permissions.SendMessages | Permissions.ReadMessageHistory,
                    Permissions.None
                );

                if (suggestion.MessageId != null)
                {
                    var msg = await channel.GetMessageAsync(suggestion.MessageId.Value);

                    var embed = new DiscordEmbedBuilder()
                        .WithTitle($"📬 Sugestão #{suggestion.Id} (FECHADA PARA DEBATE)")
                        .WithDescription(suggestion.Content)
                        .WithFooter($"Por {authorMember.Username}#{authorMember.Discriminator}", authorMember.AvatarUrl)
                        .WithTimestamp(suggestion.Timestamp)
                        .WithColor(DiscordColor.Gray)
                        .AddField("👍 Likes", suggestion.Likes.ToString(), true)
                        .AddField("👎 Dislikes", suggestion.Dislikes.ToString(), true);

                    await msg.ModifyAsync(new DiscordMessageBuilder().WithEmbed(embed));
                }
            }
        }

        private async Task HandleSuggestionSubmission(DiscordUser user, string content)
        {
            var suggestion = _service.AddSuggestion(user.Id, content);

            var guild = _client.Guilds[_config.ServerId];
            var category = guild.GetChannel(_config.CategorySugestsId);

            var sanitizedName = new string([.. content
                .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
                .Take(30)]).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(sanitizedName))
                sanitizedName = $"sugestao-{suggestion.Id}";

            var channelName = $"sugestao-{suggestion.Id}-{sanitizedName}";
            var authorMember = await guild.GetMemberAsync(suggestion.AuthorId);
            var suggestionChannel = await guild.CreateChannelAsync(channelName, ChannelType.Text, category);

            suggestion.ChannelId = suggestionChannel.Id;

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"📬 Sugestão #{suggestion.Id}")
                .WithDescription(suggestion.Content)
                .WithFooter($"Por {authorMember.Username}#{authorMember.Discriminator}", authorMember.AvatarUrl)
                .WithTimestamp(suggestion.Timestamp)
                .WithColor(DiscordColor.Azure)
                .AddField("👍 Likes", "0", true)
                .AddField("👎 Dislikes", "0", true);

            var likeButton = new DiscordButtonComponent(ButtonStyle.Success, $"like_{suggestion.Id}", "👍 0");
            var dislikeButton = new DiscordButtonComponent(ButtonStyle.Danger, $"dislike_{suggestion.Id}", "👎 0");
            var debateButton = new DiscordButtonComponent(ButtonStyle.Secondary, $"debate_{suggestion.Id}", "📢 Debater com staff");

            var messageBuilder = new DiscordMessageBuilder()
                .WithEmbed(embed)
                .AddComponents(likeButton, dislikeButton, debateButton);

            var message = await suggestionChannel.SendMessageAsync(messageBuilder);

            suggestion.MessageId = message.Id;
        }
    }
}
