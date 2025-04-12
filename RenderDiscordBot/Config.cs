using Google.Cloud.Firestore;
using RenderDiscordBot;

public class Config
{
    public required string Token { get; set; }
    public ulong ComandsChannelId { get; set; }
    public ulong MusicVoiceId { get; set; }
    public string CommandPrefix { get; set; } = "!";
    public bool EnableDms { get; set; } = false;
    public bool EnableMentionPrefix { get; set; } = true;
    public required string CLIENT_ID { get; set; }
    public required string CLIENT_SECRET { get; set; }
}

public static class ConfigService
{
    public static async Task<Config> GetConfigFromFirestoreAsync()
    {
        CollectionReference configCollection = FirebaseService.FirestoreDb.Collection("configurations");
        DocumentReference configDoc = configCollection.Document("default");

        DocumentSnapshot snapshot = await configDoc.GetSnapshotAsync();

        if (!snapshot.Exists)
            throw new Exception("Documento de configuração não encontrado no Firestore.");

        var data = snapshot.ToDictionary();

        var config = new Config
        {
            Token = data["Token"].ToString(),
            ComandsChannelId = Convert.ToUInt64(data["ComandsChannelId"].ToString()),
            MusicVoiceId = Convert.ToUInt64(data["MusicVoiceId"].ToString()),
            CommandPrefix = data["CommandPrefix"].ToString(),
            EnableDms = Convert.ToBoolean(data["EnableDms"]),
            EnableMentionPrefix = Convert.ToBoolean(data["EnableMentionPrefix"]),
            CLIENT_ID = data["CLIENT_ID"].ToString(),
            CLIENT_SECRET = data["CLIENT_SECRET"].ToString()
        };

        return config;
    }
}