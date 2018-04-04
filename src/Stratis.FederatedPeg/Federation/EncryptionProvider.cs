using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Stratis.FederatedPeg
{
    //Todo: Recommend this is reviewed or replaced with a known method used in the Full Node. 
    /// <summary>
    /// This class wraps the .Net RijndaelManaged encryption provider with password.
    /// It is used to encrypt private keys for storage on disk.
    /// </summary>
    internal static class EncryptionProvider
    {
        private const string initVector = "oejbowkztq7kj03j";
        private const int keysize = 256;

        /// <summary>
        /// Encrypts a string using a password.
        /// </summary>
        /// <param name="plainText">The string to encrypt.</param>
        /// <param name="passPhrase">The secret.</param>
        /// <returns>The cipher.</returns>
        public static string EncryptString(string plainText, string passPhrase)
        {
            byte[] initVectorBytes = Encoding.UTF8.GetBytes(initVector);
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, null);
            byte[] keyBytes = password.GetBytes(keysize / 8);
            RijndaelManaged symmetricKey = new RijndaelManaged();
            symmetricKey.Mode = CipherMode.CBC;
            ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes);
            MemoryStream memoryStream = new MemoryStream();
            CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
            cryptoStream.FlushFinalBlock();
            byte[] cipherTextBytes = memoryStream.ToArray();
            memoryStream.Close();
            cryptoStream.Close();
            return Convert.ToBase64String(cipherTextBytes);
        }

        /// <summary>
        /// Decrypts a cipher string given the password used to encrypt the original plaintext.
        /// </summary>
        /// <param name="cipherText">The encrypted text to be decrypted.</param>
        /// <param name="passPhrase">The password used to encrypt the message.</param>
        /// <returns>The original plaintext.</returns>
        public static string DecryptString(string cipherText, string passPhrase)
        {
            byte[] initVectorBytes = Encoding.UTF8.GetBytes(initVector);
            byte[] cipherTextBytes = Convert.FromBase64String(cipherText);
            PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, null);
            byte[] keyBytes = password.GetBytes(keysize / 8);
            RijndaelManaged symmetricKey = new RijndaelManaged();
            symmetricKey.Mode = CipherMode.CBC;
            ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes);
            MemoryStream memoryStream = new MemoryStream(cipherTextBytes);
            CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            byte[] plainTextBytes = new byte[cipherTextBytes.Length];
            int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
            memoryStream.Close();
            cryptoStream.Close();
            return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
        }
    }
}
