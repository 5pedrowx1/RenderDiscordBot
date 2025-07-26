using DSharpPlus;
using Google.Cloud.Firestore;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;

namespace DiscordBot
{
    class AdmCommands : BaseCommandModule
    {
        private readonly DiscordClient _client;
        private static Config _config = null!;

        public AdmCommands(DiscordClient client, Config config)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        private static async Task EnsureConfigLoadedAsync()
        {
            _config ??= await ConfigService.GetConfigFromFirestoreAsync();
        }

        private async Task<bool> ValidateCommandUsage(CommandContext ctx)
        {
            await EnsureConfigLoadedAsync();

            return true;
        }

        [Command("reiniciar")]
        public async Task ReiniciarBot(CommandContext ctx)
        {
            if (!await ValidateCommandUsage(ctx))
                return;

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

            await ctx.RespondAsync("🔄 Reiniciando bot...");
            Environment.Exit(0);
        }

        [Command("statusBot")]
        public async Task StatusAsync(CommandContext ctx)
        {
            if (!await ValidateCommandUsage(ctx))
                return;

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

            var uptime = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
            var embed = new DiscordEmbedBuilder
            {
                Title = "📊 Status do Bot",
                Description = $"**Uptime:** {uptime.Hours}h {uptime.Minutes}m\n" +
                              $"**Usuários:** {_client.Guilds.Sum(g => g.Value.MemberCount)}\n" +
                              $"**Servidores:** {_client.Guilds.Count}",
                Color = DiscordColor.Blurple
            };

            await ctx.RespondAsync(embed: embed);
        }

        [Command("limpar")]
        public async Task LimparAsync(CommandContext ctx, int quantidade)
        {
            if (!await ValidateCommandUsage(ctx))
                return;

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

            var mensagens = await ctx.Channel.GetMessagesAsync(quantidade + 1);
            await ctx.Channel.DeleteMessagesAsync(mensagens);
        }
        [Command("avisar")]
        public async Task AvisarAsync(CommandContext ctx, DiscordMember usuario, [RemainingText] string motivo)
        {
            if (!await ValidateCommandUsage(ctx))
                return;

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
                    await ctx.Channel.SendMessageAsync(embed: erroPermissao.Build());
                    return;
                }
            }

            await ctx.Message.DeleteAsync();

            var avisoData = new Dictionary<string, object>
            {
                { "moderador", ctx.User.Username },
                { "motivo", motivo },
                { "data", Timestamp.GetCurrentTimestamp() }
            };

            var userDoc = FirebaseService.FirestoreDb!.Collection("avisos").Document(usuario.Id.ToString());
            await userDoc.Collection("logs").AddAsync(avisoData);

            try
            {
                await usuario.SendMessageAsync($"⚠️ Você foi avisado por {ctx.User.Username}.\n**Motivo:** {motivo}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao enviar mensagem direta: {ex.Message}");
            }

            var logs = await userDoc.Collection("logs").GetSnapshotAsync();
            int quantidadeAvisos = logs.Count;

            if (quantidadeAvisos >= 3)
            {
                await usuario.BanAsync(0, "Acumulou 3 ou mais avisos");
                await ctx.RespondAsync($"🚫 {usuario.Mention} foi banido por acumular {quantidadeAvisos} avisos.");
                return;
            }
            else
            {
                await ctx.RespondAsync($"⚠️ {usuario.Mention} foi avisado.\n**Motivo:** {motivo}\nAvisos acumulados: {quantidadeAvisos}.");
            }
        }

        [Command("veravisos")]
        public async Task VerAvisosAsync(CommandContext ctx, DiscordMember usuario)
        {
            if (!await ValidateCommandUsage(ctx)) return;

            var userDoc = FirebaseService.FirestoreDb!.Collection("avisos").Document(usuario.Id.ToString());
            var logs = await userDoc.Collection("logs").GetSnapshotAsync();

            int quantidadeAvisos = logs.Count;

            var embed = new DiscordEmbedBuilder
            {
                Title = $"Avisos de {usuario.Username}",
                Description = $"**Número de avisos:** {quantidadeAvisos}",
                Color = DiscordColor.Blurple
            };

            if (quantidadeAvisos > 0)
            {
                embed.AddField("Últimos Avisos", string.Join("\n", logs.Select(log =>
                {
                    var motivo = log.GetValue<string>("motivo");
                    string dataAvisoStr;
                    if (log.ContainsField("data"))
                    {
                        var dataAviso = log.GetValue<Timestamp>("data");
                        dataAvisoStr = dataAviso.ToDateTime().ToString("dd/MM/yyyy HH:mm:ss");
                    }
                    else
                    {
                        dataAvisoStr = "Data inválida";
                    }
                    return $"- {motivo} ({dataAvisoStr})";
                })));
            }
            else
            {
                embed.Description = $"**{usuario.Username}** não tem avisos.";
            }

            await ctx.RespondAsync(embed: embed.Build());
        }

        [Command("mute")]
        public async Task MuteAsync(CommandContext ctx, DiscordMember usuario, string tempoStr, [RemainingText] string motivo)
        {
            if (!await ValidateCommandUsage(ctx))
                return;

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

            var muteRole = ctx.Guild.GetRole(_config.MuteRoleId);
            if (muteRole == null)
            {
                await ctx.RespondAsync("❌ Cargo 'Castigo' não encontrado.");
                return;
            }

            TimeSpan tempo = System.Text.RegularExpressions.Regex.IsMatch(tempoStr, @"^\d+[smhd]$")
                ? ParseTempo(tempoStr)
                : TimeSpan.Zero;

            await usuario.GrantRoleAsync(muteRole, motivo);
            await ctx.RespondAsync($"🔇 {usuario.Mention} foi mutado por `{tempoStr}`.\n**Motivo:** {motivo}");

            if (tempo > TimeSpan.Zero)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(tempo);
                    await usuario.RevokeRoleAsync(muteRole, "Desmute automático");
                });
            }
        }

        private TimeSpan ParseTempo(string input)
        {
            int valor = int.Parse(new string([.. input.TakeWhile(char.IsDigit)]));
            char unidade = input.Last();

            return unidade switch
            {
                's' => TimeSpan.FromSeconds(valor),
                'm' => TimeSpan.FromMinutes(valor),
                'h' => TimeSpan.FromHours(valor),
                'd' => TimeSpan.FromDays(valor),
                _ => TimeSpan.Zero
            };
        }

        [Command("unmute")]
        public async Task UnmuteAsync(CommandContext ctx, DiscordMember usuario)
        {
            if (!await ValidateCommandUsage(ctx))
                return;

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

            var muteRole = ctx.Guild.GetRole(_config.MuteRoleId);
            if (muteRole == null)
            {
                await ctx.RespondAsync("❌ Cargo 'Castigo' não encontrado.");
                return;
            }

            await usuario.RevokeRoleAsync(muteRole, "Desmutado manualmente");
            await ctx.RespondAsync($"🔈 {usuario.Mention} foi desmutado.");
        }
    }
}
