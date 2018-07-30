using System;
using System.Linq;
using NBitcoin.DataEncoders;

namespace NBitcoin
{
    /// <summary>
    /// Base58 representation of a pubkey hash and base class for the representation of a script hash
    /// </summary>
    public class BitcoinPubKeyAddress : BitcoinAddress, IBase58Data
    {
        public BitcoinPubKeyAddress(string base58, Network expectedNetwork)
            : base(Validate(base58, expectedNetwork), expectedNetwork)
        {
            byte[] decoded = Encoders.Base58Check.DecodeData(base58);
            this._KeyId = new KeyId(new uint160(decoded.Skip(expectedNetwork.GetVersionBytes(Base58Type.PUBKEY_ADDRESS, true).Length).ToArray()));
        }

        private static string Validate(string base58, Network expectedNetwork)
        {
            if (IsValid(base58, expectedNetwork))
                return base58;
            throw new FormatException("Invalid BitcoinPubKeyAddress");
        }

        public static bool IsValid(string base58, Network expectedNetwork)
        {
            if (base58 == null)
                throw new ArgumentNullException("base58");
            byte[] data = Encoders.Base58Check.DecodeData(base58);
            byte[] versionBytes = expectedNetwork.GetVersionBytes(Base58Type.PUBKEY_ADDRESS, false);
            if (versionBytes != null && data.StartWith(versionBytes))
            {
                if (data.Length == versionBytes.Length + 20)
                {
                    return true;
                }
            }
            return false;
        }

        public BitcoinPubKeyAddress(KeyId keyId, Network network) :
            base(NotNull(keyId) ?? Network.CreateBase58(Base58Type.PUBKEY_ADDRESS, keyId.ToBytes(), network), network)
        {
            this._KeyId = keyId;
        }

        private static string NotNull(KeyId keyId)
        {
            if(keyId == null)
                throw new ArgumentNullException("keyId");
            return null;
        }

        public bool VerifyMessage(string message, string signature)
        {
            PubKey key = PubKey.RecoverFromMessage(message, signature);
            return key.Hash == this.Hash;
        }

        private KeyId _KeyId;
        public KeyId Hash
        {
            get
            {
                return this._KeyId;
            }
        }


        public Base58Type Type
        {
            get
            {
                return Base58Type.PUBKEY_ADDRESS;
            }
        }

        protected override Script GeneratePaymentScript()
        {
            return PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey((KeyId)this.Hash);
        }
    }
}
