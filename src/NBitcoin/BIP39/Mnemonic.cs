using System;
using System.Collections;
using System.Linq;
using System.Text;
using NBitcoin.BouncyCastle.Crypto.Parameters;
using NBitcoin.Crypto;

namespace NBitcoin
{
    /// <summary>
    /// A .NET implementation of the Bitcoin Improvement Proposal - 39 (BIP39)
    /// BIP39 specification used as reference located here: https://github.com/bitcoin/bips/blob/master/bip-0039.mediawiki
    /// Made by thashiznets@yahoo.com.au
    /// v1.0.1.1
    /// I ♥ Bitcoin :)
    /// Bitcoin:1ETQjMkR1NNh4jwLuN5LxY7bMsHC9PUPSV
    /// </summary>
    public class Mnemonic
    {
        public Mnemonic(string mnemonic, Wordlist wordlist = null)
        {
            if(mnemonic == null)
                throw new ArgumentNullException("mnemonic");
            this._Mnemonic = mnemonic.Trim();

            if(wordlist == null)
                wordlist = Wordlist.AutoDetect(mnemonic) ?? Wordlist.English;

            string[] words = mnemonic.Split(new char[] { ' ', '　' }, StringSplitOptions.RemoveEmptyEntries);
            //if the sentence is not at least 12 characters or cleanly divisible by 3, it is bad!
            if(!CorrectWordCount(words.Length))
            {
                throw new FormatException("Word count should be equals to 12,15,18,21 or 24");
            }

            this._Words = words;
            this._WordList = wordlist;
            this._Indices = wordlist.ToIndices(words);
        }

        /// <summary>
        /// Generate a mnemonic
        /// </summary>
        /// <param name="wordList"></param>
        /// <param name="entropy"></param>
        public Mnemonic(Wordlist wordList, byte[] entropy = null)
        {
            wordList = wordList ?? Wordlist.English;
            this._WordList = wordList;
            if(entropy == null)
                entropy = RandomUtils.GetBytes(32);

            int i = Array.IndexOf(entArray, entropy.Length * 8);
            if(i == -1)
                throw new ArgumentException("The length for entropy should be : " + String.Join(",", entArray), "entropy");

            int cs = csArray[i];
            byte[] checksum = Hashes.SHA256(entropy);
            var entcsResult = new BitWriter();

            entcsResult.Write(entropy);
            entcsResult.Write(checksum, cs);
            this._Indices = entcsResult.ToIntegers();
            this._Words = this._WordList.GetWords(this._Indices);
            this._Mnemonic = this._WordList.GetSentence(this._Indices);
        }

        public Mnemonic(Wordlist wordList, WordCount wordCount)
            : this(wordList, GenerateEntropy(wordCount))
        {

        }

        private static byte[] GenerateEntropy(WordCount wordCount)
        {
            int ms = (int)wordCount;
            if(!CorrectWordCount(ms))
                throw new ArgumentException("Word count should be equal to 12,15,18,21 or 24", "wordCount");
            int i = Array.IndexOf(msArray, (int)wordCount);
            return RandomUtils.GetBytes(entArray[i] / 8);
        }

        private static readonly int[] msArray = new[] { 12, 15, 18, 21, 24 };
        private static readonly int[] csArray = new[] { 4, 5, 6, 7, 8 };
        private static readonly int[] entArray = new[] { 128, 160, 192, 224, 256 };

        private bool? _IsValidChecksum;
        public bool IsValidChecksum
        {
            get
            {
                if(this._IsValidChecksum == null)
                {
                    int i = Array.IndexOf(msArray, this._Indices.Length);
                    int cs = csArray[i];
                    int ent = entArray[i];

                    var writer = new BitWriter();
                    BitArray bits = Wordlist.ToBits(this._Indices);
                    writer.Write(bits, ent);
                    byte[] entropy = writer.ToBytes();
                    byte[] checksum = Hashes.SHA256(entropy);

                    writer.Write(checksum, cs);
                    int[] expectedIndices = writer.ToIntegers();
                    this._IsValidChecksum = expectedIndices.SequenceEqual(this._Indices);
                }
                return this._IsValidChecksum.Value;
            }
        }

        private static bool CorrectWordCount(int ms)
        {
            return msArray.Any(_ => _ == ms);
        }

        private readonly Wordlist _WordList;
        public Wordlist WordList
        {
            get
            {
                return this._WordList;
            }
        }

        private readonly int[] _Indices;
        public int[] Indices
        {
            get
            {
                return this._Indices;
            }
        }
        private readonly string[] _Words;
        public string[] Words
        {
            get
            {
                return this._Words;
            }
        }

        public byte[] DeriveSeed(string passphrase = null)
        {
            passphrase = passphrase ?? "";
            byte[] salt = Concat(Encoding.UTF8.GetBytes("mnemonic"), Normalize(passphrase));
            byte[] bytes = Normalize(this._Mnemonic);

#if NETCORE
            var mac = new NBitcoin.BouncyCastle.Crypto.Macs.HMac(new NBitcoin.BouncyCastle.Crypto.Digests.Sha512Digest());
            mac.Init(new KeyParameter(bytes));
            return Pbkdf2.ComputeDerivedKey(mac, salt, 2048, 64);
#else
            return Pbkdf2.ComputeDerivedKey(new HMACSHA512(bytes), salt, 2048, 64);
#endif

        }

        internal static byte[] Normalize(string str)
        {
            return Encoding.UTF8.GetBytes(NormalizeString(str));
        }

        internal static string NormalizeString(string word)
        {
            return KDTable.NormalizeKD(word);
        }

        public ExtKey DeriveExtKey(string passphrase = null)
        {
            return new ExtKey(DeriveSeed(passphrase));
        }

        private static Byte[] Concat(Byte[] source1, Byte[] source2)
        {
            //Most efficient way to merge two arrays this according to http://stackoverflow.com/questions/415291/best-way-to-combine-two-or-more-byte-arrays-in-c-sharp
            var buffer = new Byte[source1.Length + source2.Length];
            Buffer.BlockCopy(source1, 0, buffer, 0, source1.Length);
            Buffer.BlockCopy(source2, 0, buffer, source1.Length, source2.Length);

            return buffer;
        }


        private string _Mnemonic;
        public override string ToString()
        {
            return this._Mnemonic;
        }


    }
    public enum WordCount : int
    {
        Twelve = 12,
        Fifteen = 15,
        Eighteen = 18,
        TwentyOne = 21,
        TwentyFour = 24
    }
}