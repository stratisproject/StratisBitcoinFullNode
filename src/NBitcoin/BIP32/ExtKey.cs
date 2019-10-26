using System;
using System.Linq;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;

namespace NBitcoin
{
    /// <summary>
    /// A private Hierarchical Deterministic key
    /// </summary>
    public class ExtKey : IBitcoinSerializable, IDestination, ISecret
    {
        /// <summary>
        /// Parses the Base58 data (checking the network if specified), checks it represents the
        /// correct type of item, and then returns the corresponding ExtKey.
        /// </summary>
        public static ExtKey Parse(string wif, Network expectedNetwork = null)
        {
            return Network.Parse<BitcoinExtKey>(wif, expectedNetwork).ExtKey;
        }

        private const int FingerprintLength = 4;
        private const int ChainCodeLength = 32;

        private Key key;
        private byte[] vchChainCode = new byte[ChainCodeLength];
        private uint nChild;
        private byte nDepth;
        private byte[] vchFingerprint = new byte[FingerprintLength];

        private static readonly byte[] hashkey = Encoders.ASCII.DecodeData("Bitcoin seed");

        /// <summary>
        /// Gets the depth of this extended key from the root key.
        /// </summary>
        public byte Depth
        {
            get
            {
                return this.nDepth;
            }
        }

        /// <summary>
        /// Gets the child number of this key (in reference to the parent).
        /// </summary>
        public uint Child
        {
            get
            {
                return this.nChild;
            }
        }

        public byte[] ChainCode
        {
            get
            {
                var chainCodeCopy = new byte[ChainCodeLength];
                Buffer.BlockCopy(this.vchChainCode, 0, chainCodeCopy, 0, ChainCodeLength);

                return chainCodeCopy;
            }
        }

        /// <summary>
        /// Constructor. Reconstructs an extended key from the Base58 representations of 
        /// the public key and corresponding private key.  
        /// </summary>
        public ExtKey(BitcoinExtPubKey extPubKey, BitcoinSecret key)
            : this(extPubKey.ExtPubKey, key.PrivateKey)
        {
        }

        /// <summary>
        /// Constructor. Creates an extended key from the public key and corresponding private key.  
        /// </summary>
        /// <remarks>
        /// <para>
        /// The ExtPubKey has the relevant values for child number, depth, chain code, and fingerprint.
        /// </para>
        /// </remarks>
        public ExtKey(ExtPubKey extPubKey, Key privateKey)
        {
            if(extPubKey == null)
                throw new ArgumentNullException("extPubKey");
            if(privateKey == null)
                throw new ArgumentNullException("privateKey");
            this.nChild = extPubKey.nChild;
            this.nDepth = extPubKey.nDepth;
            this.vchChainCode = extPubKey.vchChainCode;
            this.vchFingerprint = extPubKey.vchFingerprint;
            this.key = privateKey;
        }

        /// <summary>
        /// Constructor. Creates an extended key from the private key, and specified values for
        /// chain code, depth, fingerprint, and child number.
        /// </summary>
        public ExtKey(Key key, byte[] chainCode, byte depth, byte[] fingerprint, uint child)
        {
            if(key == null)
                throw new ArgumentNullException("key");
            if(chainCode == null)
                throw new ArgumentNullException("chainCode");
            if(fingerprint == null)
                throw new ArgumentNullException("fingerprint");
            if(fingerprint.Length != FingerprintLength)
                throw new ArgumentException(string.Format("The fingerprint must be {0} bytes.", FingerprintLength), "fingerprint");
            if(chainCode.Length != ChainCodeLength)
                throw new ArgumentException(string.Format("The chain code must be {0} bytes.", ChainCodeLength), "chainCode");
            this.key = key;
            this.nDepth = depth;
            this.nChild = child;
            Buffer.BlockCopy(fingerprint, 0, this.vchFingerprint, 0, FingerprintLength);
            Buffer.BlockCopy(chainCode, 0, this.vchChainCode, 0, ChainCodeLength);
        }

        /// <summary>
        /// Constructor. Creates an extended key from the private key, with the specified value
        /// for chain code. Depth, fingerprint, and child number, will have their default values.
        /// </summary>
        public ExtKey(Key masterKey, byte[] chainCode)
        {
            if(masterKey == null)
                throw new ArgumentNullException("masterKey");
            if(chainCode == null)
                throw new ArgumentNullException("chainCode");
            if(chainCode.Length != ChainCodeLength)
                throw new ArgumentException(string.Format("The chain code must be {0} bytes.", ChainCodeLength), "chainCode");
            this.key = masterKey;
            Buffer.BlockCopy(chainCode, 0, this.vchChainCode, 0, ChainCodeLength);
        }

        /// <summary>
        /// Constructor. Creates a new extended key with a random 64 byte seed.
        /// </summary>
        public ExtKey()
        {
            byte[] seed = RandomUtils.GetBytes(64);
            SetMaster(seed);
        }

        /// <summary>
        /// Constructor. Creates a new extended key from the specified seed bytes, from the given hex string.
        /// </summary>
        public ExtKey(string seedHex)
        {
            SetMaster(Encoders.Hex.DecodeData(seedHex));
        }

        /// <summary>
        /// Constructor. Creates a new extended key from the specified seed bytes.
        /// </summary>
        public ExtKey(byte[] seed)
        {
            SetMaster(seed.ToArray());
        }

        private void SetMaster(byte[] seed)
        {
            byte[] hashMAC = Hashes.HMACSHA512(hashkey, seed);
            this.key = new Key(hashMAC.SafeSubarray(0, 32));

            Buffer.BlockCopy(hashMAC, 32, this.vchChainCode, 0, ChainCodeLength);
        }

        /// <summary>
        /// Get the private key of this extended key.
        /// </summary>
        public Key PrivateKey
        {
            get
            {
                return this.key;
            }
        }

        /// <summary>
        /// Create the public key from this key.
        /// </summary>
        public ExtPubKey Neuter()
        {
            var ret = new ExtPubKey
            {
                nDepth = this.nDepth,
                vchFingerprint = this.vchFingerprint.ToArray(),
                nChild = this.nChild,
                pubkey = this.key.PubKey,
                vchChainCode = this.vchChainCode.ToArray()
            };
            return ret;
        }

        public bool IsChildOf(ExtKey parentKey)
        {
            if(this.Depth != parentKey.Depth + 1)
                return false;
            return parentKey.CalculateChildFingerprint().SequenceEqual(this.Fingerprint);
        }
        public bool IsParentOf(ExtKey childKey)
        {
            return childKey.IsChildOf(this);
        }
        private byte[] CalculateChildFingerprint()
        {
            return this.key.PubKey.Hash.ToBytes().SafeSubarray(0, FingerprintLength);
        }

        public byte[] Fingerprint
        {
            get
            {
                return this.vchFingerprint;
            }
        }

        /// <summary>
        /// Derives a new extended key in the hierarchy as the given child number.
        /// </summary>
        public ExtKey Derive(uint index)
        {
            var result = new ExtKey
            {
                nDepth = (byte)(this.nDepth + 1),
                vchFingerprint = CalculateChildFingerprint(),
                nChild = index
            };
            result.key = this.key.Derivate(this.vchChainCode, index, out result.vchChainCode);
            return result;
        }

        /// <summary>
        /// Derives a new extended key in the hierarchy as the given child number, 
        /// setting the high bit if hardened is specified.
        /// </summary>
        public ExtKey Derive(int index, bool hardened)
        {
            if(index < 0)
                throw new ArgumentOutOfRangeException("index", "the index can't be negative");
            uint realIndex = (uint)index;
            realIndex = hardened ? realIndex | 0x80000000u : realIndex;
            return Derive(realIndex);
        }

        /// <summary>
        /// Derives a new extended key in the hierarchy at the given path below the current key,
        /// by deriving the specified child at each step.
        /// </summary>
        public ExtKey Derive(KeyPath derivation)
        {
            ExtKey result = this;
            return derivation.Indexes.Aggregate(result, (current, index) => current.Derive(index));
        }

        /// <summary>
        /// Converts the extended key to the base58 representation, within the specified network.
        /// </summary>
        public BitcoinExtKey GetWif(Network network)
        {
            return new BitcoinExtKey(this, network);
        }

        #region IBitcoinSerializable Members

        public void ReadWrite(BitcoinStream stream)
        {
            using(stream.BigEndianScope())
            {
                stream.ReadWrite(ref this.nDepth);
                stream.ReadWrite(ref this.vchFingerprint);
                stream.ReadWrite(ref this.nChild);
                stream.ReadWrite(ref this.vchChainCode);
                byte b = 0;
                stream.ReadWrite(ref b);
                stream.ReadWrite(ref this.key);
            }
        }

        #endregion

        /// <summary>
        /// Converts the extended key to the base58 representation, as a string, within the specified network.
        /// </summary>
        public string ToString(Network network)
        {
            return new BitcoinExtKey(this, network).ToString();
        }

        #region IDestination Members

        /// <summary>
        /// Gets the script of the hash of the public key corresponding to the private key.
        /// </summary>
        public Script ScriptPubKey
        {
            get
            {
                return this.PrivateKey.PubKey.Hash.ScriptPubKey;
            }
        }

        #endregion

        /// <summary>
        /// Gets whether or not this extended key is a hardened child.
        /// </summary>
        public bool IsHardened
        {
            get
            {
                return (this.nChild & 0x80000000u) != 0;
            }
        }

        /// <summary>
        /// Recreates the private key of the parent from the private key of the child 
        /// combinated with the public key of the parent (hardened children cannot be
        /// used to recreate the parent).
        /// </summary>
        public ExtKey GetParentExtKey(ExtPubKey parent)
        {
            if(parent == null)
                throw new ArgumentNullException("parent");
            if(this.Depth == 0)
                throw new InvalidOperationException("This ExtKey is the root key of the HD tree");
            if(this.IsHardened)
                throw new InvalidOperationException("This private key is hardened, so you can't get its parent");
            byte[] expectedFingerPrint = parent.CalculateChildFingerprint();
            if(parent.Depth != this.Depth - 1 || !expectedFingerPrint.SequenceEqual(this.vchFingerprint))
                throw new ArgumentException("The parent ExtPubKey is not the immediate parent of this ExtKey", "parent");

            byte[] l = null;
            var ll = new byte[32];
            var lr = new byte[32];

            byte[] pubKey = parent.PubKey.ToBytes();
            l = Hashes.BIP32Hash(parent.vchChainCode, this.nChild, pubKey[0], pubKey.SafeSubarray(1));
            Array.Copy(l, ll, 32);
            Array.Copy(l, 32, lr, 0, 32);
            byte[] ccChild = lr;

            var parse256LL = new BigInteger(1, ll);
            BigInteger N = ECKey.CURVE.N;

            if(!ccChild.SequenceEqual(this.vchChainCode))
                throw new InvalidOperationException("The derived chain code of the parent is not equal to this child chain code");

            byte[] keyBytes = this.PrivateKey.ToBytes();
            var key = new BigInteger(1, keyBytes);

            BigInteger kPar = key.Add(parse256LL.Negate()).Mod(N);
            byte[] keyParentBytes = kPar.ToByteArrayUnsigned();
            if(keyParentBytes.Length < 32)
                keyParentBytes = new byte[32 - keyParentBytes.Length].Concat(keyParentBytes).ToArray();

            var parentExtKey = new ExtKey
            {
                vchChainCode = parent.vchChainCode,
                nDepth = parent.Depth,
                vchFingerprint = parent.Fingerprint,
                nChild = parent.nChild,
                key = new Key(keyParentBytes)
            };
            return parentExtKey;
        }

    }
}
