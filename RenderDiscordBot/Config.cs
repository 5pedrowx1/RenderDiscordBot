using Google.Cloud.Firestore;

namespace RenderDiscordBot
{
    public class Config
    {
        public required string Token { get; set; }
        public ulong ComandsChannelId { get; set; }
        public ulong MusicVoiceId { get; set; }
        public ulong MemberRoleId { get; set; }
        public ulong EntryChannelId { get; set; }
        public ulong BoostChannelId { get; set; }
        public ulong CategorySuportId { get; set; }
        public ulong AdminRoleId { get; set; }
        public ulong ServerId { get; set; }
        public ulong VoiceCreateId { get; set; }
        public ulong CategoryVoiceId { get; set; }
        public ulong MiniRPGChannelId { get; set; }
        public ulong MuteRoleId { get; set; }
        public ulong NewsChannelId { get; set; }
        public string CommandPrefix { get; set; } = "!";
        public string? UrlLive {  get; set; }
        public bool EnableDms { get; set; } = false;
        public bool EnableMentionPrefix { get; set; } = true;
        public required string CLIENT_ID { get; set; }
        public required string CLIENT_SECRET { get; set; }
    }

    public static class ConfigService
    {
        public static async Task<Config> GetConfigFromFirestoreAsync()
        {
            CollectionReference configCollection = FirebaseService.FirestoreDb!.Collection("configurations");
            DocumentReference configDoc = configCollection.Document("default");

            DocumentSnapshot snapshot = await configDoc.GetSnapshotAsync();

            if (!snapshot.Exists)
                throw new Exception("Documento de configuração não encontrado no Firestore.");

            var data = snapshot.ToDictionary();

            if (!data.TryGetValue("Token", out var Token) || Token == null)
                throw new Exception("Token não encontrado na configuração.");
            if (!data.TryGetValue("ComandsChannelId", out var ComandsChannelId) || ComandsChannelId == null)
                throw new Exception("ComandsChannelId não encontrado na configuração.");
            if (!data.TryGetValue("MusicVoiceId", out var MusicVoiceId) || MusicVoiceId == null)
                throw new Exception("MusicVoiceId não encontrado na configuração.");
            if (!data.TryGetValue("MemberRoleId", out var MemberRoleId) || MemberRoleId == null)
                throw new Exception("MemberRoleId não encontrado na configuração.");
            if (!data.TryGetValue("EntryChannelId", out var EntryChannelId) || EntryChannelId == null)
                throw new Exception("EntryChannelId não encontrado na configuração.");
            if (!data.TryGetValue("BoostChannelId", out var BoostChannelId) || BoostChannelId == null)
                throw new Exception("BoostChannelId não encontrado na configuração.");
            if (!data.TryGetValue("CommandPrefix", out var CommandPrefix) || CommandPrefix == null)
                throw new Exception("CommandPrefix não encontrado na configuração.");
            if (!data.TryGetValue("EnableDms", out var EnableDms) || EnableDms == null)
                throw new Exception("EnableDms não encontrado na configuração.");
            if (!data.TryGetValue("EnableMentionPrefix", out var EnableMentionPrefix) || EnableMentionPrefix == null)
                throw new Exception("EnableMentionPrefix não encontrado na configuração.");
            if (!data.TryGetValue("CLIENT_ID", out var CLIENT_ID) || CLIENT_ID == null)
                throw new Exception("CLIENT_ID não encontrado na configuração.");
            if (!data.TryGetValue("CLIENT_SECRET", out var CLIENT_SECRET) || CLIENT_SECRET == null)
                throw new Exception("CLIENT_SECRET não encontrado na configuração.");
            if (!data.TryGetValue("CategorySuportId", out var CategorySuportId) || CategorySuportId == null)
                throw new Exception("CategorySuportId não encontrado na configuração");
            if (!data.TryGetValue("AdminRoleId", out var AdminRoleId) || AdminRoleId == null)
                throw new Exception("AdminRoleId não encontrado na configuração");
            if (!data.TryGetValue("ServerId", out var ServerId) || ServerId == null)
                throw new Exception("ServerId não encontrado na configuração");
            if (!data.TryGetValue("VoiceCreateId", out var VoiceCreateId) || VoiceCreateId == null)
                throw new Exception("VoiceCreateId não encontrado na configuração");
            if (!data.TryGetValue("CategoryVoiceId", out var CategoryVoiceId) || CategoryVoiceId == null)
                throw new Exception("CategoryVoiceId não encontrado na configuração");
            if (!data.TryGetValue("MiniRPGChannelId", out var MiniRPGChannelId) || MiniRPGChannelId == null)
                throw new Exception("MiniRPGChannelId não encontrado na configuração");
            if (!data.TryGetValue("MuteRoleId", out var MuteRoleId) || MuteRoleId == null)
                throw new Exception("MuteRoleId não encontrado na configuração");
            if (!data.TryGetValue("UrlLive", out var UrlLive) || UrlLive == null)
                throw new Exception("UrlLive não encontrado na configuração");
            if (!data.TryGetValue("NewsChannelId", out var NewsChannelId) || NewsChannelId == null)
                throw new Exception("NewsChannelId não encontrado na configuração");

            var config = new Config
            {
                Token = Token.ToString()!,
                ComandsChannelId = Convert.ToUInt64(ComandsChannelId.ToString()),
                MusicVoiceId = Convert.ToUInt64(MusicVoiceId.ToString()),
                MemberRoleId = Convert.ToUInt64(MemberRoleId.ToString()),
                EntryChannelId = Convert.ToUInt64(EntryChannelId.ToString()),
                BoostChannelId = Convert.ToUInt64(BoostChannelId.ToString()),
                CategorySuportId = Convert.ToUInt64(CategorySuportId.ToString()),
                CategoryVoiceId = Convert.ToUInt64(CategoryVoiceId.ToString()),
                AdminRoleId = Convert.ToUInt64(AdminRoleId.ToString()),
                ServerId = Convert.ToUInt64(ServerId.ToString()),
                VoiceCreateId = Convert.ToUInt64(VoiceCreateId.ToString()),
                MiniRPGChannelId = Convert.ToUInt64(MiniRPGChannelId.ToString()),
                MuteRoleId = Convert.ToUInt64(MuteRoleId.ToString()),
                NewsChannelId = Convert.ToUInt64(NewsChannelId.ToString()),
                CommandPrefix = CommandPrefix.ToString()!,
                UrlLive = UrlLive.ToString()!,
                EnableDms = Convert.ToBoolean(EnableDms),
                EnableMentionPrefix = Convert.ToBoolean(EnableMentionPrefix),
                CLIENT_ID = CLIENT_ID.ToString()!,
                CLIENT_SECRET = CLIENT_SECRET.ToString()!
            };
            return config;
        }
    }
}
