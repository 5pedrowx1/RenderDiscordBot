using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace DiscordBot
{
    public class EntryHandler
    {
        private readonly DiscordClient _client;
        private readonly Config _config;

        public EntryHandler(DiscordClient client, Config config)
        {
            _client = client;
            _config = config;
        }

        public async Task OnUserJoinedAsync(DiscordClient sender, GuildMemberAddEventArgs e)
        {
            var user = e.Member;
            Console.WriteLine($"Novo usuário {user.Username} entrou no servidor.");

            ulong roleId = _config.MemberRoleId;
            var role = user.Guild.GetRole(roleId);
            if (role == null)
            {
                Console.WriteLine($"Cargo com ID {roleId} não encontrado.");
                return;
            }

            var botUser = await user.Guild.GetMemberAsync(_client.CurrentUser.Id);

            var anyChannel = user.Guild.Channels.Values.FirstOrDefault();
            if (anyChannel == null || !botUser.PermissionsIn(anyChannel).HasPermission(Permissions.ManageRoles))
            {
                Console.WriteLine("O bot não tem permissão para gerenciar cargos.");
                return;
            }

            var botHighestRole = botUser.Roles.OrderByDescending(r => r.Position).FirstOrDefault();
            if (botHighestRole == null || botHighestRole.Position <= role.Position)
            {
                Console.WriteLine("O cargo do bot não está acima do cargo a ser atribuído.");
                return;
            }

            await user.GrantRoleAsync(role);
            Console.WriteLine($"Cargo {role.Name} atribuído ao usuário {user.Username}.");

            var welcomeEmbed = new DiscordEmbedBuilder
            {
                Title = "🎉 Boas-vindas ao servidor!",
                Description = $"Olá {user.Mention}, seja bem-vindo ao servidor! Estamos felizes em tê-lo conosco. 😄",
                Color = DiscordColor.Green,
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail { Url = user.AvatarUrl ?? user.DefaultAvatarUrl }
            };
            welcomeEmbed.WithFooter("Seja ativo e aproveite a nossa comunidade! 🎮");

            var welcomeChannel = user.Guild.GetChannel(_config.EntryChannelId);
            if (welcomeChannel != null)
            {
                await welcomeChannel.SendMessageAsync(embed: welcomeEmbed);
            }
            else
            {
                Console.WriteLine("Canal de boas-vindas não encontrado.");
            }
        }

        public async Task OnUserUpdatedAsync(DiscordClient sender, GuildMemberUpdateEventArgs e)
        {
            var memberBefore = e.MemberBefore;
            var memberAfter = e.Member;

            bool hadBoostBefore = memberBefore?.PremiumSince.HasValue ?? false;
            bool hasBoostNow = memberAfter.PremiumSince.HasValue;

            if (!hadBoostBefore && hasBoostNow)
            {
                Console.WriteLine($"Usuário {memberAfter.Username} deu um novo boost no servidor.");

                var boostEmbed = new DiscordEmbedBuilder
                {
                    Title = "🚀 Novo Server Boost!",
                    Description = $"{memberAfter.Mention} deu um **boost** no servidor! 🎉 Obrigado pelo seu apoio! 💖",
                    Color = DiscordColor.Purple,
                    Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail { Url = memberAfter.AvatarUrl ?? memberAfter.DefaultAvatarUrl }
                };

                boostEmbed.WithFooter("Vamos continuar crescendo juntos!");

                var boostChannel = memberAfter.Guild.GetChannel(_config.BoostChannelId);
                if (boostChannel != null)
                {
                    await boostChannel.SendMessageAsync(embed: boostEmbed);
                }
                else
                {
                    Console.WriteLine("Canal de boost não encontrado.");
                }
            }
        }
    }
}
