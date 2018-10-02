using System;
using System.IO;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

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

        public Key GeneratePrivateKey()
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            Key privKey = mnemonic.DeriveExtKey().PrivateKey;

            return privKey;
        }

        public string GetPrivateKeySavePath()
        {
            string path = Path.Combine(this.dataFolder.RootPath, KeyTool.KeyFileDefaultName);

            return path;
        }

        public void SavePrivateKey(Key privKey)
        {
            var ms = new MemoryStream();
            var stream = new BitcoinStream(ms, true);
            stream.ReadWrite(ref privKey);

            ms.Seek(0, SeekOrigin.Begin);
            FileStream fileStream = File.Create(this.GetPrivateKeySavePath());
            ms.CopyTo(fileStream);
            fileStream.Close();
        }

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
