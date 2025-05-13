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
        public List<string> UrlLives { get; set; } = [];
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

            string token = GetField<string>(data, "Token");
            ulong commandsChannelId = GetField<ulong>(data, "ComandsChannelId");
            ulong musicVoiceId = GetField<ulong>(data, "MusicVoiceId");
            ulong memberRoleId = GetField<ulong>(data, "MemberRoleId");
            ulong entryChannelId = GetField<ulong>(data, "EntryChannelId");
            ulong boostChannelId = GetField<ulong>(data, "BoostChannelId");
            ulong categorySuportId = GetField<ulong>(data, "CategorySuportId");
            ulong adminRoleId = GetField<ulong>(data, "AdminRoleId");
            ulong serverId = GetField<ulong>(data, "ServerId");
            ulong voiceCreateId = GetField<ulong>(data, "VoiceCreateId");
            ulong categoryVoiceId = GetField<ulong>(data, "CategoryVoiceId");
            ulong miniRPGChannelId = GetField<ulong>(data, "MiniRPGChannelId");
            ulong muteRoleId = GetField<ulong>(data, "MuteRoleId");
            ulong newsChannelId = GetField<ulong>(data, "NewsChannelId");

            string commandPrefix = GetField<string>(data, "CommandPrefix");
            bool enableDms = GetField<bool>(data, "EnableDms");
            bool enableMentionPrefix = GetField<bool>(data, "EnableMentionPrefix");
            string clientId = GetField<string>(data, "CLIENT_ID");
            string clientSecret = GetField<string>(data, "CLIENT_SECRET");

            var urlLives = new List<string>();
            if (!data.TryGetValue("UrlLives", out var urlLivesObj) || urlLivesObj == null)
                throw new Exception("UrlLives não encontrado na configuração.");
            if (urlLivesObj is IEnumerable<object> listRaw)
            {
                foreach (var item in listRaw)
                {
                    if (item != null)
                        urlLives.Add(item.ToString()!);
                }
            }
            else
            {
                throw new Exception("UrlLives está em formato inválido no Firestore.");
            }

            return new Config
            {
                Token = token,
                ComandsChannelId = commandsChannelId,
                MusicVoiceId = musicVoiceId,
                MemberRoleId = memberRoleId,
                EntryChannelId = entryChannelId,
                BoostChannelId = boostChannelId,
                CategorySuportId = categorySuportId,
                AdminRoleId = adminRoleId,
                ServerId = serverId,
                VoiceCreateId = voiceCreateId,
                CategoryVoiceId = categoryVoiceId,
                MiniRPGChannelId = miniRPGChannelId,
                MuteRoleId = muteRoleId,
                NewsChannelId = newsChannelId,
                CommandPrefix = commandPrefix,
                EnableDms = enableDms,
                EnableMentionPrefix = enableMentionPrefix,
                CLIENT_ID = clientId,
                CLIENT_SECRET = clientSecret,
                UrlLives = urlLives
            };
        }

        private static T GetField<T>(Dictionary<string, object> data, string fieldName)
        {
            if (!data.TryGetValue(fieldName, out var value) || value == null)
                throw new Exception($"{fieldName} não encontrado na configuração.");

            return (T)Convert.ChangeType(value, typeof(T));
        }
    }
}
