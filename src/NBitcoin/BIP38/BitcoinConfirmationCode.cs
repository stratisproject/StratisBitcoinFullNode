using System;
using System.Linq;
using NBitcoin.BouncyCastle.Asn1.X9;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.BouncyCastle.Math.EC;
using NBitcoin.Crypto;

namespace NBitcoin
{
    public class BitcoinConfirmationCode : Base58Data
    {

        public BitcoinConfirmationCode(string wif, Network expectedNetwork = null)
            : base(wif, expectedNetwork)
        {
        }
        public BitcoinConfirmationCode(byte[] rawBytes, Network network)
            : base(rawBytes, network)
        {
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

        private byte[] _OwnerEntropy;
        public byte[] OwnerEntropy
        {
            get
            {
                return this._OwnerEntropy ?? (this._OwnerEntropy = this.vchData.SafeSubarray(5, 8));
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
                if(this._LotSequence == null)
                {
                    this._LotSequence = new LotSequence(this.OwnerEntropy.SafeSubarray(4, 4));
                }
                return this._LotSequence;
            }
        }

        private byte[] _EncryptedPointB;

        private byte[] EncryptedPointB
        {
            get
            {
                return this._EncryptedPointB ?? (this._EncryptedPointB = this.vchData.SafeSubarray(13));
            }
        }

        public override Base58Type Type
        {
            get
            {
                return Base58Type.CONFIRMATION_CODE;
            }
        }

        protected override bool IsValid
        {
            get
            {
                return this.vchData.Length == 1 + 4 + 8 + 33;
            }
        }


        public bool Check(string passphrase, BitcoinAddress expectedAddress)
        {
            //Derive passfactor using scrypt with ownerentropy and the user's passphrase and use it to recompute passpoint 
            byte[] passfactor = BitcoinEncryptedSecretEC.CalculatePassFactor(passphrase, this.LotSequence, this.OwnerEntropy);
            //Derive decryption key for pointb using scrypt with passpoint, addresshash, and ownerentropy
            byte[] passpoint = BitcoinEncryptedSecretEC.CalculatePassPoint(passfactor);
            byte[] derived = BitcoinEncryptedSecretEC.CalculateDecryptionKey(passpoint, this.AddressHash, this.OwnerEntropy);

            //Decrypt encryptedpointb to yield pointb
            byte pointbprefix = this.EncryptedPointB[0];
            pointbprefix = (byte)(pointbprefix ^ (byte)(derived[63] & (byte)0x01));

            //Optional since ArithmeticException will catch it, but it saves some times
            if(pointbprefix != 0x02 && pointbprefix != 0x03)
                return false;
            byte[] pointb = BitcoinEncryptedSecret.DecryptKey(this.EncryptedPointB.Skip(1).ToArray(), derived);
            pointb = new byte[] { pointbprefix }.Concat(pointb).ToArray();

            //4.ECMultiply pointb by passfactor. Use the resulting EC point as a public key
            X9ECParameters curve = ECKey.Secp256k1;
            ECPoint pointbec;
            try
            {
                pointbec = curve.Curve.DecodePoint(pointb);
            }
            catch(ArgumentException)
            {
                return false;
            }
            catch(ArithmeticException)
            {
                return false;
            }
            var pubkey = new PubKey(pointbec.Multiply(new BigInteger(1, passfactor)).GetEncoded());

            //and hash it into address using either compressed or uncompressed public key methodology as specifid in flagbyte.
            pubkey = this.IsCompressed ? pubkey.Compress() : pubkey.Decompress();

            byte[] actualhash = BitcoinEncryptedSecretEC.HashAddress(pubkey.GetAddress(this.Network));
            byte[] expectedhash = BitcoinEncryptedSecretEC.HashAddress(expectedAddress);

            return Utils.ArrayEqual(actualhash, expectedhash);
        }
    }
}