using System.IO;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA
{
    public class KeyTool
    {
        public const string KeyFileDefaultName = "federationKey.dat";

        private readonly string path;

        public KeyTool(DataFolder dataFolder)
        {
            this.path = dataFolder.RootPath;
        }

        public KeyTool(string path)
        {
            this.path = path;
        }

        /// <summary>Generates a new private key.</summary>
        public Key GeneratePrivateKey()
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            Key privateKey = mnemonic.DeriveExtKey().PrivateKey;

            return privateKey;
        }

        /// <summary>Gets the default path for private key saving and loading.</summary>
        public string GetPrivateKeySavePath()
        {
            string path = Path.Combine(this.path, KeyTool.KeyFileDefaultName);

            return path;
        }

        /// <summary>Saves private key to default path.</summary>
        public void SavePrivateKey(Key privateKey)
        {
            using (var ms = new MemoryStream())
            {
                var stream = new BitcoinStream(ms, true);
                stream.ReadWrite(ref privateKey);

                ms.Seek(0, SeekOrigin.Begin);

                using (FileStream fileStream = File.Create(this.GetPrivateKeySavePath()))
                {
                    ms.CopyTo(fileStream);
                }
            }
        }

        /// <summary>Loads the private key from default path.</summary>
        public Key LoadPrivateKey()
        {
            string path = this.GetPrivateKeySavePath();

            if (!File.Exists(path))
                return null;

            using (FileStream readStream = File.OpenRead(path))
            {
                var privateKey = new Key();
                var stream = new BitcoinStream(readStream, false);
                stream.ReadWrite(ref privateKey);

                return privateKey;
            }
        }
    }
}
