using System;
using System.Linq;
using NBitcoin.DataEncoders;

namespace NBitcoin
{
    public class BitcoinColoredAddress : Base58Data, IDestination
    {
        public BitcoinColoredAddress(string base58, Network expectedNetwork = null)
            : base(base58, expectedNetwork)
        {
        }

        public BitcoinColoredAddress(BitcoinAddress address)
            : base(Build(address), address.Network)
        {

        }

        private static byte[] Build(BitcoinAddress address)
        {
            if(address is IBase58Data)
            {
                var b58 = (IBase58Data)address;
                byte[] version = address.Network.GetVersionBytes(b58.Type, true);
                byte[] data = Encoders.Base58Check.DecodeData(b58.ToString()).Skip(version.Length).ToArray();
                return version.Concat(data).ToArray();
            }
            else
            {
                throw new NotSupportedException("Building a colored address out of a non base58 string is not supported");
            }
        }

        protected override bool IsValid
        {
            get
            {
                return this.Address != null;
            }
        }

        private BitcoinAddress _Address;
        public BitcoinAddress Address
        {
            get
            {
                if(this._Address == null)
                {
                    string base58 = Encoders.Base58Check.EncodeData(this.vchData);
                    this._Address = BitcoinAddress.Create(base58, this.Network);
                }
                return this._Address;
            }
        }

        public override Base58Type Type
        {
            get
            {
                return Base58Type.COLORED_ADDRESS;
            }
        }

        #region IDestination Members

        public Script ScriptPubKey
        {
            get
            {
                return this.Address.ScriptPubKey;
            }
        }

        #endregion

        public static string GetWrappedBase58(string base58, Network network)
        {
            byte[] coloredVersion = network.GetVersionBytes(Base58Type.COLORED_ADDRESS, true);
            byte[] inner = Encoders.Base58Check.DecodeData(base58);
            inner = inner.Skip(coloredVersion.Length).ToArray();
            return Encoders.Base58Check.EncodeData(inner);
        }
    }
}
