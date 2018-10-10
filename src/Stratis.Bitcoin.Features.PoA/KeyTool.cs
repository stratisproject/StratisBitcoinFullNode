using System.IO;
using NBitcoin;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.Features.PoA
{
    public class KeyTool
    {
        public const string KeyFileDefaultName = "federationKey.dat";

        private readonly DataFolder dataFolder;

        public KeyTool(DataFolder dataFolder)
        {
            this.dataFolder = dataFolder;
        }

        /// <summary>Generates new private key.</summary>
        public Key GeneratePrivateKey()
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            Key privateKey = mnemonic.DeriveExtKey().PrivateKey;

            return privateKey;
        }

        /// <summary>Gets default path for private key saving and loading.</summary>
        public string GetPrivateKeySavePath()
        {
            string path = Path.Combine(this.dataFolder.RootPath, KeyTool.KeyFileDefaultName);

            return path;
        }

        /// <summary>Saves private key to default path.</summary>
        public void SavePrivateKey(Key privateKey)
        {
            var ms = new MemoryStream();
            var stream = new BitcoinStream(ms, true);
            stream.ReadWrite(ref privateKey);

            ms.Seek(0, SeekOrigin.Begin);
            FileStream fileStream = File.Create(this.GetPrivateKeySavePath());
            ms.CopyTo(fileStream);
            fileStream.Close();
        }

        /// <summary>Loads private key from default path.</summary>
        public Key LoadPrivateKey()
        {
            string path = this.GetPrivateKeySavePath();

            if (!File.Exists(path))
                return null;

            FileStream readStream = File.OpenRead(path);

            var privKey = new Key();
            var stream = new BitcoinStream(readStream, false);
            stream.ReadWrite(ref privKey);
            readStream.Close();

            return privKey;
        }
    }
}
