using System;
using System.Linq;
using NBitcoin.DataEncoders;
using NBitcoin.Networks;

namespace NBitcoin
{

    public interface IBase58Data : IBitcoinString
    {
        Base58Type Type
        {
            get;
        }
    }

    /// <summary>
    /// Base class for all Base58 check representation of data
    /// </summary>
    public abstract class Base58Data : IBase58Data
    {
        protected byte[] vchData = new byte[0];
        protected byte[] vchVersion = new byte[0];
        protected string wifData = "";
        private Network _Network;
        public Network Network
        {
            get
            {
                return this._Network;
            }
        }

        protected Base58Data(string base64, Network expectedNetwork = null)
        {
            this._Network = expectedNetwork;
            SetString(base64);
        }

        protected Base58Data(byte[] rawBytes, Network network)
        {
            if(network == null)
                throw new ArgumentNullException("network");
            this._Network = network;
            SetData(rawBytes);
        }

        private void SetString(string base64)
        {
            if(this._Network == null)
            {
                this._Network = NetworkRegistration.GetNetworkFromBase58Data(base64, this.Type);
                if(this._Network == null)
                    throw new FormatException("Invalid " + GetType().Name);
            }

            byte[] vchTemp = Encoders.Base58Check.DecodeData(base64);
            byte[] expectedVersion = this._Network.GetVersionBytes(this.Type, true);


            this.vchVersion = vchTemp.SafeSubarray(0, expectedVersion.Length);
            if(!Utils.ArrayEqual(this.vchVersion, expectedVersion))
                throw new FormatException("The version prefix does not match the expected one " + String.Join(",", expectedVersion));

            this.vchData = vchTemp.SafeSubarray(expectedVersion.Length);
            this.wifData = base64;

            if(!this.IsValid)
                throw new FormatException("Invalid " + GetType().Name);

        }


        private void SetData(byte[] vchData)
        {
            this.vchData = vchData;
            this.vchVersion = this._Network.GetVersionBytes(this.Type, true);
            this.wifData = Encoders.Base58Check.EncodeData(this.vchVersion.Concat(vchData).ToArray());

            if(!this.IsValid)
                throw new FormatException("Invalid " + GetType().Name);
        }


        protected virtual bool IsValid
        {
            get
            {
                return true;
            }
        }

        public abstract Base58Type Type
        {
            get;
        }



        public string ToWif()
        {
            return this.wifData;
        }
        public byte[] ToBytes()
        {
            return this.vchData.ToArray();
        }
        public override string ToString()
        {
            return this.wifData;
        }

        public override bool Equals(object obj)
        {
            var item = obj as Base58Data;
            if(item == null)
                return false;
            return ToString().Equals(item.ToString());
        }
        public static bool operator ==(Base58Data a, Base58Data b)
        {
            if(ReferenceEquals(a, b))
                return true;
            if(((object)a == null) || ((object)b == null))
                return false;
            return a.ToString() == b.ToString();
        }

        public static bool operator !=(Base58Data a, Base58Data b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }
}
