using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using System.Text;

namespace RenderDiscordBot
{
    public static class FirebaseService
    {
        private static bool _initialized = false;
        public static FirestoreDb? FirestoreDb { get; private set; }

        public static void InitializeFirebase(string encryptionKey)
        {
            if (_initialized)
                return;

            string encryptedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "serviceAccountKey.enc");

            if (!File.Exists(encryptedPath))
                throw new FileNotFoundException("Chave criptografada do Firebase não encontrada!", encryptedPath);

            string decryptedJson = SecureFileManager.DecryptJson(encryptedPath, encryptionKey);

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(decryptedJson));
            GoogleCredential credential = GoogleCredential.FromStream(stream);

            if (FirebaseApp.DefaultInstance == null)
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = credential,
                    ProjectId = "renderdiscordbot"
                });
            }

            FirestoreDb = new FirestoreDbBuilder
            {
                ProjectId = "renderdiscordbot",
                Credential = credential
            }.Build();

            _initialized = true;
        }
    }
}
