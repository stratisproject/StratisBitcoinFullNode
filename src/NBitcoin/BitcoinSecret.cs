using System.Collections.Generic;
using System.Linq;
using NBitcoin.DataEncoders;

namespace NBitcoin
{
    public class BitcoinSecret : Base58Data, IDestination, ISecret
    {
        public BitcoinSecret(Key key, Network network)
            : base(ToBytes(key), network)
        {
        }

        private static byte[] ToBytes(Key key)
        {
            byte[] keyBytes = key.ToBytes();
            if(!key.IsCompressed)
                return keyBytes;
            else
                return keyBytes.Concat(new byte[] { 0x01 }).ToArray();
        }
        public BitcoinSecret(string base58, Network expectedAddress = null)
            : base(base58, expectedAddress)
        {
        }

        private BitcoinPubKeyAddress _address;

        public BitcoinPubKeyAddress GetAddress()
        {
            return this._address ?? (this._address = this.PrivateKey.PubKey.GetAddress(this.Network));
        }

        public virtual KeyId PubKeyHash
        {
            get
            {
                return this.PrivateKey.PubKey.Hash;
            }
        }

        public PubKey PubKey
        {
            get
            {
                return this.PrivateKey.PubKey;
            }
        }

        #region ISecret Members

        private Key _Key;
        public Key PrivateKey
        {
            get
            {
                return this._Key ?? (this._Key = new Key(this.vchData, 32, this.IsCompressed));
            }
        }
        #endregion

        protected override bool IsValid
        {
            get
            {
                if(this.vchData.Length != 33 && this.vchData.Length != 32)
                    return false;

                if(this.vchData.Length == 33 && this.IsCompressed)
                    return true;
                if(this.vchData.Length == 32 && !this.IsCompressed)
                    return true;
                return false;
            }
        }

        public BitcoinEncryptedSecret Encrypt(string password)
        {
            return this.PrivateKey.GetEncryptedBitcoinSecret(password, this.Network);
        }


        public BitcoinSecret Copy(bool? compressed)
        {
            if(compressed == null)
                compressed = this.IsCompressed;

            if(compressed.Value && this.IsCompressed)
            {
                return new BitcoinSecret(this.wifData, this.Network);
            }
            else
            {
                byte[] result = Encoders.Base58Check.DecodeData(this.wifData);
                List<byte> resultList = result.ToList();

                if(compressed.Value)
                {
                    resultList.Insert(resultList.Count, 0x1);
                }
                else
                {
                    resultList.RemoveAt(resultList.Count - 1);
                }
                return new BitcoinSecret(Encoders.Base58Check.EncodeData(resultList.ToArray()), this.Network);
            }
        }

        public bool IsCompressed
        {
            get
            {
                return this.vchData.Length > 32 && this.vchData[32] == 1;
            }
        }

        public override Base58Type Type
        {
            get
            {
                return Base58Type.SECRET_KEY;
            }
        }

        #region IDestination Members

        public Script ScriptPubKey
        {
            get
            {
                return GetAddress().ScriptPubKey;
            }
        }

        #endregion


    }
}
