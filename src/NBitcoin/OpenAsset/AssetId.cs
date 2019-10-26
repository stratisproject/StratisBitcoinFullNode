using System;
using NBitcoin.DataEncoders;

namespace NBitcoin.OpenAsset
{
    /// <summary>
    /// A unique Id for an asset
    /// </summary>
    public class AssetId
    {
        internal byte[] _Bytes;

        public AssetId()
        {
            this._Bytes = new byte[] { 0 };
        }

        public AssetId(IDestination assetScriptPubKey)
            : this(assetScriptPubKey.ScriptPubKey)
        {
            if(assetScriptPubKey == null)
                throw new ArgumentNullException("assetScriptPubKey");
        }

        public AssetId(BitcoinAssetId assetId)
        {
            if(assetId == null)
                throw new ArgumentNullException("assetId");
            this._Bytes = assetId.AssetId._Bytes;
        }

        public AssetId(Script assetScriptPubKey)
            : this(assetScriptPubKey.Hash)
        {
            if(assetScriptPubKey == null)
                throw new ArgumentNullException("assetScriptPubKey");
        }

        public AssetId(ScriptId scriptId)
        {
            if(scriptId == null)
                throw new ArgumentNullException("scriptId");
            this._Bytes = scriptId.ToBytes(true);
        }

        public AssetId(byte[] value)
        {
            if(value == null)
                throw new ArgumentNullException("value");
            this._Bytes = value;
        }
        public AssetId(uint160 value)
            : this(value.ToBytes())
        {
        }

        public AssetId(string value)
        {
            this._Bytes = Encoders.Hex.DecodeData(value);
            this._Str = value;
        }

        public BitcoinAssetId GetWif(Network network)
        {
            if(network == null)
                throw new ArgumentNullException("network");
            return new BitcoinAssetId(this, network);
        }

        public byte[] ToBytes()
        {
            return ToBytes(false);
        }
        public byte[] ToBytes(bool @unsafe)
        {
            if(@unsafe)
                return this._Bytes;
            var array = new byte[this._Bytes.Length];
            Array.Copy(this._Bytes, array, this._Bytes.Length);
            return array;
        }

        public override bool Equals(object obj)
        {
            var item = obj as AssetId;
            if(item == null)
                return false;
            return Utils.ArrayEqual(this._Bytes, item._Bytes);
        }
        public static bool operator ==(AssetId a, AssetId b)
        {
            if(ReferenceEquals(a, b))
                return true;
            if(((object)a == null) || ((object)b == null))
                return false;
            return Utils.ArrayEqual(a._Bytes, b._Bytes);
        }

        public static bool operator !=(AssetId a, AssetId b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return Utils.GetHashCode(this._Bytes);
        }

        private string _Str;
        public override string ToString()
        {
            if(this._Str == null) this._Str = Encoders.Hex.EncodeData(this._Bytes);
            return this._Str;
        }

        public string ToString(Network network)
        {
            if(network == null)
                throw new ArgumentNullException("network");
            return new BitcoinAssetId(this, network).ToString();
        }
    }
}
