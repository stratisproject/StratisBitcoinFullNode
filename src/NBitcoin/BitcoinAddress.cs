using System;
using System.Linq;
using NBitcoin.DataEncoders;

namespace NBitcoin
{
    /// <summary>
    /// Base58 representation of a script hash
    /// </summary>
    public class BitcoinScriptAddress : BitcoinAddress, IBase58Data
    {
        public BitcoinScriptAddress(string base58, Network expectedNetwork)
            : base(Validate(base58, expectedNetwork), expectedNetwork)
        {
            byte[] decoded = Encoders.Base58Check.DecodeData(base58);
            this._Hash = new ScriptId(new uint160(decoded.Skip(expectedNetwork.GetVersionBytes(Base58Type.SCRIPT_ADDRESS, true).Length).ToArray()));
        }

        private static string Validate(string base58, Network expectedNetwork)
        {
            if (IsValid(base58, expectedNetwork))
                return base58;
            throw new FormatException("Invalid BitcoinScriptAddress");
        }

        public static bool IsValid(string base58, Network expectedNetwork)
        {
            if (base58 == null)
                throw new ArgumentNullException("base58");
            byte[] data = Encoders.Base58Check.DecodeData(base58);
            byte[] versionBytes = expectedNetwork.GetVersionBytes(Base58Type.SCRIPT_ADDRESS, false);
            if (versionBytes != null && data.StartWith(versionBytes))
            {
                if (data.Length == versionBytes.Length + 20)
                {
                    return true;
                }
            }
            return false;
        }

        public BitcoinScriptAddress(ScriptId scriptId, Network network)
            : base(NotNull(scriptId) ?? Network.CreateBase58(Base58Type.SCRIPT_ADDRESS, scriptId.ToBytes(), network), network)
        {
            this._Hash = scriptId;
        }

        private static string NotNull(ScriptId scriptId)
        {
            if(scriptId == null)
                throw new ArgumentNullException("scriptId");
            return null;
        }

        private ScriptId _Hash;
        public ScriptId Hash
        {
            get
            {
                return this._Hash;
            }
        }

        public Base58Type Type
        {
            get
            {
                return Base58Type.SCRIPT_ADDRESS;
            }
        }

        protected override Script GeneratePaymentScript()
        {
            return PayToScriptHashTemplate.Instance.GenerateScriptPubKey((ScriptId) this.Hash);
        }
    }

    /// <summary>
    /// Base58 representation of a bitcoin address
    /// </summary>
    public abstract class BitcoinAddress : IDestination, IBitcoinString
    {
        /// <summary>
        /// Detect whether the input base58 is a pubkey hash or a script hash
        /// </summary>
        /// <param name="str">The string to parse</param>
        /// <param name="expectedNetwork">The expected network to which it belongs</param>
        /// <returns>A BitcoinAddress or BitcoinScriptAddress</returns>
        /// <exception cref="System.FormatException">Invalid format</exception>
        public static BitcoinAddress Create(string str, Network expectedNetwork = null)
        {
            if(str == null)
                throw new ArgumentNullException("base58");
            return Network.Parse<BitcoinAddress>(str, expectedNetwork);
        }

        public BitcoinAddress(string str, Network network)
        {
            if(network == null)
                throw new ArgumentNullException("network");
            if(str == null)
                throw new ArgumentNullException("str");
            this._Str = str;
            this._Network = network;
        }

        private string _Str;

        private Script _ScriptPubKey;
        public Script ScriptPubKey
        {
            get
            {
                if(this._ScriptPubKey == null)
                {
                    this._ScriptPubKey = GeneratePaymentScript();
                }
                return this._ScriptPubKey;
            }
        }

        protected abstract Script GeneratePaymentScript();

        public BitcoinScriptAddress GetScriptAddress()
        {
            var bitcoinScriptAddress = this as BitcoinScriptAddress;
            if(bitcoinScriptAddress != null)
                return bitcoinScriptAddress;

            return new BitcoinScriptAddress(this.ScriptPubKey.Hash, this.Network);
        }

        public BitcoinColoredAddress ToColoredAddress()
        {
            return new BitcoinColoredAddress(this);
        }


        private readonly Network _Network;
        public Network Network
        {
            get
            {
                return this._Network;
            }
        }

        public override string ToString()
        {
            return this._Str;
        }


        public override bool Equals(object obj)
        {
            var item = obj as BitcoinAddress;
            if(item == null)
                return false;
            return this._Str.Equals(item._Str);
        }
        public static bool operator ==(BitcoinAddress a, BitcoinAddress b)
        {
            if(ReferenceEquals(a, b))
                return true;
            if(((object)a == null) || ((object)b == null))
                return false;
            return a._Str == b._Str;
        }

        public static bool operator !=(BitcoinAddress a, BitcoinAddress b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return this._Str.GetHashCode();
        }
    }
}
