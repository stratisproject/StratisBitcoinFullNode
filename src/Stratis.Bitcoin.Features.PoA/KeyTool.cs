using System.IO;
using NBitcoin;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.Features.PoA
{
    public class KeyTool
    {
        public const string KeyFileDefaultName = "federationKey.dat";

        public Key GeneratePrivateKey()
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            Key privKey = mnemonic.DeriveExtKey().PrivateKey;

            return privKey;
        }

        public string GetPrivateKeyDefaultPath(NodeSettings settings)
        {
            string path = Path.Combine(settings.DataDir, KeyTool.KeyFileDefaultName);

            return path;
        }

        public void SavePrivateKey(Key privKey, string path)
        {
            var ms = new MemoryStream();
            var stream = new BitcoinStream(ms, true);
            stream.ReadWrite(ref privKey);

            ms.Seek(0, SeekOrigin.Begin);
            FileStream fileStream = File.Create(path);
            ms.CopyTo(fileStream);
            fileStream.Close();
        }

        public Key LoadPrivateKey(string path)
        {
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
