using System;

namespace NBitcoin.OpenAsset
{
    /// <summary>
    /// Base58 representation of an asset id
    /// </summary>
    public class BitcoinAssetId : Base58Data
    {
        public BitcoinAssetId(string base58, Network expectedNetwork = null)
            : base(base58, expectedNetwork)
        {
        }
        public BitcoinAssetId(byte[] raw, Network network)
            : base(raw, network)
        {
        }

        public BitcoinAssetId(AssetId assetId, Network network)
            : this(assetId._Bytes, network)
        {
            if(assetId == null)
                throw new ArgumentNullException("assetId");
            if(network == null)
                throw new ArgumentNullException("network");
        }

        private AssetId _AssetId;
        public AssetId AssetId
        {
            get
            {
                if(this._AssetId == null) this._AssetId = new AssetId(this.vchData);
                return this._AssetId;
            }
        }

        protected override bool IsValid
        {
            get
            {
                return this.vchData.Length == 20;
            }
        }

        public override Base58Type Type
        {
            get
            {
                return Base58Type.ASSET_ID;
            }
        }

        public static implicit operator AssetId(BitcoinAssetId id)
        {
            if(id == null)
                return null;
            return id.AssetId;
        }
    }
}
