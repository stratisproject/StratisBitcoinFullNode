using System.Linq;
using System.Security;
using System.Text;
using NBitcoin.BouncyCastle.Asn1.X9;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using System.Security.Cryptography;

namespace NBitcoin
{
    public class BitcoinEncryptedSecretNoEC : BitcoinEncryptedSecret
    {

        public BitcoinEncryptedSecretNoEC(string wif, Network expectedNetwork = null)
            : base(wif, expectedNetwork)
        {
        }

        public BitcoinEncryptedSecretNoEC(byte[] raw, Network network)
            : base(raw, network)
        {
        }

        public BitcoinEncryptedSecretNoEC(Key key, string password, Network network)
            : base(GenerateWif(key, password, network), network)
        {

        }

        private static string GenerateWif(Key key, string password, Network network)
        {
            byte[] vch = key.ToBytes();
            //Compute the Bitcoin address (ASCII),
            byte[] addressBytes = Encoders.ASCII.DecodeData(key.PubKey.GetAddress(network).ToString());
            // and take the first four bytes of SHA256(SHA256()) of it. Let's call this "addresshash".
            byte[] addresshash = Hashes.Hash256(addressBytes).ToBytes().SafeSubarray(0, 4);

            byte[] derived = SCrypt.BitcoinComputeDerivedKey(Encoding.UTF8.GetBytes(password), addresshash);

            byte[] encrypted = EncryptKey(vch, derived);



            byte[] version = network.GetVersionBytes(Base58Type.ENCRYPTED_SECRET_KEY_NO_EC, true);
            byte flagByte = 0;
            flagByte |= 0x0C0;
            flagByte |= (key.IsCompressed ? (byte)0x20 : (byte)0x00);

            byte[] bytes = version
                            .Concat(new[] { flagByte })
                            .Concat(addresshash)
                            .Concat(encrypted).ToArray();
            return Encoders.Base58Check.EncodeData(bytes);
        }

        private byte[] _FirstHalf;
        public byte[] EncryptedHalf1
        {
            get
            {
                return this._FirstHalf ?? (this._FirstHalf = this.vchData.SafeSubarray(this.ValidLength - 32, 16));
            }
        }

        private byte[] _Encrypted;
        public byte[] Encrypted
        {
            get
            {
                return this._Encrypted ?? (this._Encrypted = this.EncryptedHalf1.Concat(this.EncryptedHalf2).ToArray());
            }
        }

        public override Base58Type Type
        {
            get
            {
                return Base58Type.ENCRYPTED_SECRET_KEY_NO_EC;
            }
        }

        public override Key GetKey(string password)
        {
            byte[] derived = SCrypt.BitcoinComputeDerivedKey(password, this.AddressHash);
            byte[] bitcoinprivkey = DecryptKey(this.Encrypted, derived);

            var key = new Key(bitcoinprivkey, fCompressedIn: this.IsCompressed);

            byte[] addressBytes = Encoders.ASCII.DecodeData(key.PubKey.GetAddress(this.Network).ToString());
            byte[] salt = Hashes.Hash256(addressBytes).ToBytes().SafeSubarray(0, 4);

            if(!Utils.ArrayEqual(salt, this.AddressHash))
                throw new SecurityException("Invalid password (or invalid Network)");
            return key;
        }


    }

    public class DecryptionResult
    {
        public Key Key
        {
            get;
            set;
        }
        public LotSequence LotSequence
        {
            get;
            set;
        }
    }
    public class BitcoinEncryptedSecretEC : BitcoinEncryptedSecret
    {

        public BitcoinEncryptedSecretEC(string wif, Network expectedNetwork = null)
            : base(wif, expectedNetwork)
        {
        }

        public BitcoinEncryptedSecretEC(byte[] raw, Network network)
            : base(raw, network)
        {
        }

        private byte[] _OwnerEntropy;
        public byte[] OwnerEntropy
        {
            get
            {
                return this._OwnerEntropy ?? (this._OwnerEntropy = this.vchData.SafeSubarray(this.ValidLength - 32, 8));
            }
        }

        private LotSequence _LotSequence;
        public LotSequence LotSequence
        {
            get
            {
                bool hasLotSequence = (this.vchData[0] & 0x04) != 0;
                if(!hasLotSequence)
                    return null;
                return this._LotSequence ?? (this._LotSequence = new LotSequence(this.OwnerEntropy.SafeSubarray(4, 4)));
            }
        }

        private byte[] _EncryptedHalfHalf1;
        public byte[] EncryptedHalfHalf1
        {
            get
            {
                return this._EncryptedHalfHalf1 ?? (this._EncryptedHalfHalf1 = this.vchData.SafeSubarray(this.ValidLength - 32 + 8, 8));
            }
        }

        private byte[] _PartialEncrypted;
        public byte[] PartialEncrypted
        {
            get
            {
                return this._PartialEncrypted ?? (this._PartialEncrypted = this.EncryptedHalfHalf1.Concat(new byte[8]).Concat(this.EncryptedHalf2).ToArray());
            }
        }



        public override Base58Type Type
        {
            get
            {
                return Base58Type.ENCRYPTED_SECRET_KEY_EC;
            }
        }

        public override Key GetKey(string password)
        {
            byte[] encrypted = this.PartialEncrypted.ToArray();
            //Derive passfactor using scrypt with ownerentropy and the user's passphrase and use it to recompute passpoint
            byte[] passfactor = CalculatePassFactor(password, this.LotSequence, this.OwnerEntropy);
            byte[] passpoint = CalculatePassPoint(passfactor);

            byte[] derived = SCrypt.BitcoinComputeDerivedKey2(passpoint, this.AddressHash.Concat(this.OwnerEntropy).ToArray());

            //Decrypt encryptedpart1 to yield the remainder of seedb.
            byte[] seedb = DecryptSeed(encrypted, derived);
            byte[] factorb = Hashes.Hash256(seedb).ToBytes();

            X9ECParameters curve = ECKey.Secp256k1;

            //Multiply passfactor by factorb mod N to yield the private key associated with generatedaddress.
            BigInteger keyNum = new BigInteger(1, passfactor).Multiply(new BigInteger(1, factorb)).Mod(curve.N);
            byte[] keyBytes = keyNum.ToByteArrayUnsigned();
            if(keyBytes.Length < 32)
                keyBytes = new byte[32 - keyBytes.Length].Concat(keyBytes).ToArray();

            var key = new Key(keyBytes, fCompressedIn: this.IsCompressed);

            BitcoinPubKeyAddress generatedaddress = key.PubKey.GetAddress(this.Network);
            byte[] addresshash = HashAddress(generatedaddress);

            if(!Utils.ArrayEqual(addresshash, this.AddressHash))
                throw new SecurityException("Invalid password (or invalid Network)");

            return key;
        }

        /// <summary>
        /// Take the first four bytes of SHA256(SHA256(generatedaddress)) and call it addresshash.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        internal static byte[] HashAddress(BitcoinAddress address)
        {
            return Hashes.Hash256(Encoders.ASCII.DecodeData(address.ToString())).ToBytes().Take(4).ToArray();
        }

        internal static byte[] CalculatePassPoint(byte[] passfactor)
        {
            return new Key(passfactor, fCompressedIn: true).PubKey.ToBytes();
        }

        internal static byte[] CalculatePassFactor(string password, LotSequence lotSequence, byte[] ownerEntropy)
        {
            byte[] passfactor;
            if(lotSequence == null)
            {
                passfactor = SCrypt.BitcoinComputeDerivedKey(Encoding.UTF8.GetBytes(password), ownerEntropy, 32);
            }
            else
            {
                byte[] ownersalt = ownerEntropy.SafeSubarray(0, 4);
                byte[] prefactor = SCrypt.BitcoinComputeDerivedKey(Encoding.UTF8.GetBytes(password), ownersalt, 32);
                passfactor = Hashes.Hash256(prefactor.Concat(ownerEntropy).ToArray()).ToBytes();
            }
            return passfactor;
        }

        internal static byte[] CalculateDecryptionKey(byte[] Passpoint, byte[] addresshash, byte[] ownerEntropy)
        {
            return SCrypt.BitcoinComputeDerivedKey2(Passpoint, addresshash.Concat(ownerEntropy).ToArray());
        }

    }

    public abstract class BitcoinEncryptedSecret : Base58Data
    {
        public static BitcoinEncryptedSecret Create(string wif, Network expectedNetwork = null)
        {
            return Network.Parse<BitcoinEncryptedSecret>(wif, expectedNetwork);
        }

        public static BitcoinEncryptedSecretNoEC Generate(Key key, string password, Network network)
        {
            return new BitcoinEncryptedSecretNoEC(key, password, network);
        }


        protected BitcoinEncryptedSecret(byte[] raw, Network network)
            : base(raw, network)
        {
        }

        protected BitcoinEncryptedSecret(string wif, Network network)
            : base(wif, network)
        {
        }


        public bool EcMultiply
        {
            get
            {
                return this is BitcoinEncryptedSecretEC;
            }
        }


        private byte[] _AddressHash;
        public byte[] AddressHash
        {
            get
            {
                return this._AddressHash ?? (this._AddressHash = this.vchData.SafeSubarray(1, 4));
            }
        }
        public bool IsCompressed
        {
            get
            {
                return (this.vchData[0] & 0x20) != 0;
            }
        }

        private byte[] _LastHalf;
        public byte[] EncryptedHalf2
        {
            get
            {
                return this._LastHalf ?? (this._LastHalf = this.vchData.Skip(this.ValidLength - 16).ToArray());
            }
        }
        protected int ValidLength = (1 + 4 + 16 + 16);


        protected override bool IsValid
        {
            get
            {
                bool lenOk = this.vchData.Length == this.ValidLength;
                if(!lenOk)
                    return false;
                bool reserved = (this.vchData[0] & 0x10) == 0 && (this.vchData[0] & 0x08) == 0;
                return reserved;
            }
        }

        public abstract Key GetKey(string password);
        public BitcoinSecret GetSecret(string password)
        {
            return new BitcoinSecret(GetKey(password), this.Network);
        }

        internal static Aes CreateAES256()
        {
            Aes aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.ECB;
            aes.IV = new byte[16];
            return aes;
        }

        internal static byte[] EncryptKey(byte[] key, byte[] derived)
        {
            byte[] keyhalf1 = key.SafeSubarray(0, 16);
            byte[] keyhalf2 = key.SafeSubarray(16, 16);
            return EncryptKey(keyhalf1, keyhalf2, derived);
        }

        private static byte[] EncryptKey(byte[] keyhalf1, byte[] keyhalf2, byte[] derived)
        {
            byte[] derivedhalf1 = derived.SafeSubarray(0, 32);
            byte[] derivedhalf2 = derived.SafeSubarray(32, 32);

            var encryptedhalf1 = new byte[16];
            var encryptedhalf2 = new byte[16];

            Aes aes = CreateAES256();
            aes.Key = derivedhalf2;
            ICryptoTransform encrypt = aes.CreateEncryptor();

            for(int i = 0; i < 16; i++)
            {
                derivedhalf1[i] = (byte)(keyhalf1[i] ^ derivedhalf1[i]);
            }

            encrypt.TransformBlock(derivedhalf1, 0, 16, encryptedhalf1, 0);

            for(int i = 0; i < 16; i++)
            {
                derivedhalf1[16 + i] = (byte)(keyhalf2[i] ^ derivedhalf1[16 + i]);
            }
            encrypt.TransformBlock(derivedhalf1, 16, 16, encryptedhalf2, 0);

            return encryptedhalf1.Concat(encryptedhalf2).ToArray();
        }

        internal static byte[] DecryptKey(byte[] encrypted, byte[] derived)
        {
            byte[] derivedhalf1 = derived.SafeSubarray(0, 32);
            byte[] derivedhalf2 = derived.SafeSubarray(32, 32);

            byte[] encryptedHalf1 = encrypted.SafeSubarray(0, 16);
            byte[] encryptedHalf2 = encrypted.SafeSubarray(16, 16);

            var bitcoinprivkey1 = new byte[16];
            var bitcoinprivkey2 = new byte[16];

            Aes aes = CreateAES256();
            aes.Key = derivedhalf2;
            ICryptoTransform decrypt = aes.CreateDecryptor();
            //Need to call that two time, seems AES bug
            decrypt.TransformBlock(encryptedHalf1, 0, 16, bitcoinprivkey1, 0);
            decrypt.TransformBlock(encryptedHalf1, 0, 16, bitcoinprivkey1, 0);

            for(int i = 0; i < 16; i++)
            {
                bitcoinprivkey1[i] ^= derivedhalf1[i];
            }

            //Need to call that two time, seems AES bug
            decrypt.TransformBlock(encryptedHalf2, 0, 16, bitcoinprivkey2, 0);
            decrypt.TransformBlock(encryptedHalf2, 0, 16, bitcoinprivkey2, 0);

            for(int i = 0; i < 16; i++)
            {
                bitcoinprivkey2[i] ^= derivedhalf1[16 + i];
            }

            return bitcoinprivkey1.Concat(bitcoinprivkey2).ToArray();
        }


        internal static byte[] EncryptSeed(byte[] seedb, byte[] derived)
        {
            byte[] derivedhalf1 = derived.SafeSubarray(0, 32);
            byte[] derivedhalf2 = derived.SafeSubarray(32, 32);

            var encryptedhalf1 = new byte[16];
            var encryptedhalf2 = new byte[16];

            Aes aes = CreateAES256();
            aes.Key = derivedhalf2;
            ICryptoTransform encrypt = aes.CreateEncryptor();

            //AES256Encrypt(seedb[0...15] xor derivedhalf1[0...15], derivedhalf2), call the 16-byte result encryptedpart1
            for(int i = 0; i < 16; i++)
            {
                derivedhalf1[i] = (byte)(seedb[i] ^ derivedhalf1[i]);
            }

            encrypt.TransformBlock(derivedhalf1, 0, 16, encryptedhalf1, 0);

            //AES256Encrypt((encryptedpart1[8...15] + seedb[16...23]) xor derivedhalf1[16...31], derivedhalf2), call the 16-byte result encryptedpart2. The "+" operator is concatenation.
            byte[] half = encryptedhalf1.SafeSubarray(8, 8).Concat(seedb.SafeSubarray(16, 8)).ToArray();
            for(int i = 0; i < 16; i++)
            {
                derivedhalf1[16 + i] = (byte)(half[i] ^ derivedhalf1[16 + i]);
            }

            encrypt.TransformBlock(derivedhalf1, 16, 16, encryptedhalf2, 0);

            return encryptedhalf1.Concat(encryptedhalf2).ToArray();
        }

        internal static byte[] DecryptSeed(byte[] encrypted, byte[] derived)
        {
            var seedb = new byte[24];
            byte[] derivedhalf1 = derived.SafeSubarray(0, 32);
            byte[] derivedhalf2 = derived.SafeSubarray(32, 32);

            byte[] encryptedhalf2 = encrypted.SafeSubarray(16, 16);

            Aes aes = CreateAES256();
            aes.Key = derivedhalf2;
            ICryptoTransform decrypt = aes.CreateDecryptor();

            var half = new byte[16];
            //Decrypt encryptedpart2 using AES256Decrypt to yield the last 8 bytes of seedb and the last 8 bytes of encryptedpart1.

            decrypt.TransformBlock(encryptedhalf2, 0, 16, half, 0);
            decrypt.TransformBlock(encryptedhalf2, 0, 16, half, 0);

            //half = (encryptedpart1[8...15] + seedb[16...23]) xor derivedhalf1[16...31])
            for(int i = 0; i < 16; i++)
            {
                half[i] = (byte)(half[i] ^ derivedhalf1[16 + i]);
            }

            //half =  (encryptedpart1[8...15] + seedb[16...23])
            for(int i = 0; i < 8; i++)
            {
                seedb[seedb.Length - i - 1] = half[half.Length - i - 1];
            }
            //Restore missing encrypted part
            for(int i = 0; i < 8; i++)
            {
                encrypted[i + 8] = half[i];
            }
            byte[] encryptedhalf1 = encrypted.SafeSubarray(0, 16);

            decrypt.TransformBlock(encryptedhalf1, 0, 16, seedb, 0);
            decrypt.TransformBlock(encryptedhalf1, 0, 16, seedb, 0);

            //seedb = seedb[0...15] xor derivedhalf1[0...15]
            for(int i = 0; i < 16; i++)
            {
                seedb[i] = (byte)(seedb[i] ^ derivedhalf1[i]);
            }
            return seedb;
        }
    }
}