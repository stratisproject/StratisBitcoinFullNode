using NBitcoin.OpenAsset;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    public class CoinCollection : List<ICoin>
    {
        public CoinCollection()
        {

        }
        public CoinCollection(IEnumerable<ICoin> enumerable)
        {
            AddRange(enumerable);
        }
        public ICoin this[OutPoint index]
        {
            get
            {
                for (int i = 0 ; i < this.Count ; i++)
                {
                    if (this[i].Outpoint == index)
                        return this[i];
                }
                throw new KeyNotFoundException();
            }
            set
            {
                for (int i = 0 ; i < this.Count ; i++)
                {
                    if (this[i].Outpoint == index)
                    {
                        this[i] = value;
                        return;
                    }
                }
                throw new KeyNotFoundException();
            }
        }

        public IEnumerable<Coin> WhereUncolored()
        {
            return this.OfType<Coin>();
        }

        public IEnumerable<ColoredCoin> WhereColored(BitcoinAssetId assetId)
        {
            return WhereColored(assetId.AssetId);
        }
        public IEnumerable<ColoredCoin> WhereColored(AssetId assetId)
        {
            return this.OfType<ColoredCoin>().Where(c => c.AssetId == assetId);
        }
    }
}
