using Playnite.Common;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NileLibraryNS
{
    public class Helpers
    {
        public static string GetMD5(string input)
        {
            using (var md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        public static void EncryptToNileFile(string filePath, string content, string encryptionKey)
        {
            var finalEncryptionKey = encryptionKey.GetSHA256HashByte();

            using Aes cipher = Aes.Create();
            cipher.Key = finalEncryptionKey;
            cipher.Mode = CipherMode.CBC;
            cipher.Padding = PaddingMode.PKCS7;
            cipher.GenerateIV();

            var utfString = Encoding.UTF8.GetBytes(content);
            using ICryptoTransform encryptor = cipher.CreateEncryptor();
            var encryptedData = encryptor.TransformFinalBlock(utfString, 0, utfString.Length);


            using Aes ivCipher = Aes.Create();
            ivCipher.Key = finalEncryptionKey;
            ivCipher.Mode = CipherMode.ECB;
            ivCipher.Padding = PaddingMode.None;

            using ICryptoTransform ivEncryptor = ivCipher.CreateEncryptor();
            var encryptedIv = ivEncryptor.TransformFinalBlock(cipher.IV, 0, cipher.IV.Length);


            byte[] result = new byte[encryptedIv.Length + encryptedData.Length];
            Buffer.BlockCopy(encryptedIv, 0, result, 0, encryptedIv.Length);
            Buffer.BlockCopy(encryptedData, 0, result, encryptedIv.Length, encryptedData.Length);

            File.WriteAllBytes(filePath, result);
        }
    }
}
