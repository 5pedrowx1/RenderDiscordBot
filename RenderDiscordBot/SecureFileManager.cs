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
    }
}
