using System.Security.Cryptography;
using System.Text;

namespace DiscordBot
{
    public static class SecureFileManager
    {
        public static string DecryptJson(string inputPath, string encryptionKey)
        {
            byte[] encryptedData = File.ReadAllBytes(inputPath);

            byte[] salt = new byte[16];
            Array.Copy(encryptedData, 0, salt, 0, salt.Length);

            using (Aes aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var keyDerivation = new Rfc2898DeriveBytes(encryptionKey, salt, 10000);
                aes.Key = keyDerivation.GetBytes(aes.KeySize / 8);
                aes.IV = keyDerivation.GetBytes(aes.BlockSize / 8);

                using var ms = new MemoryStream(encryptedData, salt.Length, encryptedData.Length - salt.Length);
                using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
                using var sr = new StreamReader(cs, Encoding.UTF8);
                return sr.ReadToEnd();
            }
        }

        public static string EncryptJson(string jsonPath, string encryptionKey)
        {
            string plainJson = File.ReadAllText(jsonPath, Encoding.UTF8);

            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            using (Aes aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var keyDerivation = new Rfc2898DeriveBytes(encryptionKey, salt, 10000);
                aes.Key = keyDerivation.GetBytes(aes.KeySize / 8);
                aes.IV = keyDerivation.GetBytes(aes.BlockSize / 8);

                using var ms = new MemoryStream();
                ms.Write(salt, 0, salt.Length);

                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs, Encoding.UTF8))
                {
                    sw.Write(plainJson);
                }

                byte[] encryptedBytes = ms.ToArray();

                string encryptedFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "serviceAccountKey.enc");
                File.WriteAllBytes(encryptedFilePath, encryptedBytes);

                string base64Encrypted = Convert.ToBase64String(encryptedBytes);
                string encryptedBase64Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "serviceAccountKey.base64");
                File.WriteAllText(encryptedBase64Path, base64Encrypted, Encoding.UTF8);

                return base64Encrypted;
            }
        }
    }
}
