using System;
using NBitcoin.DataEncoders;

namespace NBitcoin
{
    public class BitcoinWitPubKeyAddress : BitcoinAddress, IBech32Data
    {
        public BitcoinWitPubKeyAddress(string bech32, Network expectedNetwork)
                : base(Validate(bech32, expectedNetwork), expectedNetwork)
        {
            Bech32Encoder encoder = expectedNetwork.GetBech32Encoder(Bech32Type.WITNESS_PUBKEY_ADDRESS, true);
            byte witVersion;
            byte[] decoded = encoder.Decode(bech32, out witVersion);
            this._Hash = new WitKeyId(decoded);
        }

        private static string Validate(string bech32, Network expectedNetwork)
        {
            bool isValid = IsValid(bech32, expectedNetwork, out Exception exception);

            if (exception != null)
                throw exception;

            if (isValid)
                return bech32;
            
            throw new FormatException("Invalid BitcoinWitPubKeyAddress");
        }

        public static bool IsValid(string bech32, Network expectedNetwork, out Exception exception)
        {
            exception = null;

            if (bech32 == null)
            {
                exception = new ArgumentNullException("bech32");
                return false;
            }

            Bech32Encoder encoder = expectedNetwork.GetBech32Encoder(Bech32Type.WITNESS_PUBKEY_ADDRESS, false);
            if (encoder == null)
                return false;

            try
            {
                byte witVersion;
                byte[] data = encoder.Decode(bech32, out witVersion);
                if (data.Length == 20 && witVersion == 0)
                {
                    return true;
                }
            }
            catch (Bech32FormatException bech32FormatException)
            {
                exception = bech32FormatException;
                return false;
            }
            catch (FormatException)
            {
                exception = new FormatException("Invalid BitcoinWitPubKeyAddress");
                return false;
            }

            exception = new FormatException("Invalid BitcoinWitScriptAddress");
            return false;
        }

        public BitcoinWitPubKeyAddress(WitKeyId segwitKeyId, Network network) :
            base(NotNull(segwitKeyId) ?? Network.CreateBech32(Bech32Type.WITNESS_PUBKEY_ADDRESS, segwitKeyId.ToBytes(), 0, network), network)
        {
            this._Hash = segwitKeyId;
        }

        private static string NotNull(WitKeyId segwitKeyId)
        {
            if(segwitKeyId == null)
                throw new ArgumentNullException("segwitKeyId");
            return null;
        }

        public bool VerifyMessage(string message, string signature)
        {
            PubKey key = PubKey.RecoverFromMessage(message, signature);
            return key.WitHash == this.Hash;
        }

        private WitKeyId _Hash;
        public WitKeyId Hash
        {
            get
            {
                return this._Hash;
            }
        }


        protected override Script GeneratePaymentScript()
        {
            return PayToWitTemplate.Instance.GenerateScriptPubKey(OpcodeType.OP_0, this.Hash._DestBytes);
        }

        public Bech32Type Type
        {
            get
            {
                return Bech32Type.WITNESS_PUBKEY_ADDRESS;
            }
        }
    }

    public class BitcoinWitScriptAddress : BitcoinAddress, IBech32Data
    {
        public BitcoinWitScriptAddress(string bech32, Network expectedNetwork = null)
                : base(Validate(bech32, expectedNetwork), expectedNetwork)
        {
            Bech32Encoder encoder = expectedNetwork.GetBech32Encoder(Bech32Type.WITNESS_SCRIPT_ADDRESS, true);
            byte witVersion;
            byte[] decoded = encoder.Decode(bech32, out witVersion);
            this._Hash = new WitScriptId(decoded);
        }

        private static string Validate(string bech32, Network expectedNetwork)
        {
            bool isValid = IsValid(bech32, expectedNetwork, out Exception exception);

            if (exception != null)
                throw exception;

            if (isValid)
                return bech32;

            throw new FormatException("Invalid BitcoinWitScriptAddress");
        }

        public static bool IsValid(string bech32, Network expectedNetwork, out Exception exception)
        {
            exception = null;

            if (bech32 == null)
            {
                exception = new ArgumentNullException("bech32");
                return false;
            }

            Bech32Encoder encoder = expectedNetwork.GetBech32Encoder(Bech32Type.WITNESS_SCRIPT_ADDRESS, false);
            if (encoder == null)
                return false;
            try
            {
                byte witVersion;
                byte[] data = encoder.Decode(bech32, out witVersion);
                if (data.Length == 32 && witVersion == 0)
                {
                    return true;
                }
            }
            catch (Bech32FormatException bech32FormatException)
            {
                exception = bech32FormatException;
                return false;
            }
            catch (FormatException)
            {
                exception = new FormatException("Invalid BitcoinWitPubKeyAddress");
                return false;
            }

            exception = new FormatException("Invalid BitcoinWitPubKeyAddress");
            return false;
        }

        public BitcoinWitScriptAddress(WitScriptId segwitScriptId, Network network)
    : base(NotNull(segwitScriptId) ?? Network.CreateBech32(Bech32Type.WITNESS_SCRIPT_ADDRESS, segwitScriptId.ToBytes(), 0, network), network)
        {
            this._Hash = segwitScriptId;
        }


        private static string NotNull(WitScriptId segwitScriptId)
        {
            if(segwitScriptId == null)
                throw new ArgumentNullException("segwitScriptId");
            return null;
        }

        private WitScriptId _Hash;
        public WitScriptId Hash
        {
            get
            {
                return this._Hash;
            }
        }        

        protected override Script GeneratePaymentScript()
        {
            return PayToWitTemplate.Instance.GenerateScriptPubKey(OpcodeType.OP_0, this.Hash._DestBytes);
        }

        public Bech32Type Type
        {
            get
            {
                return Bech32Type.WITNESS_SCRIPT_ADDRESS;
            }
        }
    }
}
