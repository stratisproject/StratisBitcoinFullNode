using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    public class InMemoryCoinView : CoinView
    {
        private readonly ReaderWriterLock lockobj = new ReaderWriterLock();
        readonly Dictionary<uint256, UnspentOutputs> unspents = new Dictionary<uint256, UnspentOutputs>();
        private uint256 blockHash;

        public InMemoryCoinView(uint256 blockHash)
        {
            this.blockHash = blockHash;
        }

        public override Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds)
        {
            Guard.NotNull(txIds, nameof(txIds));

            using (this.lockobj.LockRead())
            {
                UnspentOutputs[] result = new UnspentOutputs[txIds.Length];
                for (int i = 0; i < txIds.Length; i++)
                {
                    result[i] = this.unspents.TryGet(txIds[i]);
                    if (result[i] != null)
                        result[i] = result[i].Clone();
                }
                return Task.FromResult(new FetchCoinsResponse(result, this.blockHash));
            }
        }

        public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash)
        {
            Guard.NotNull(oldBlockHash, nameof(oldBlockHash));
            Guard.NotNull(nextBlockHash, nameof(nextBlockHash));
            Guard.NotNull(unspentOutputs, nameof(unspentOutputs));

            using (this.lockobj.LockWrite())
            {
                if (this.blockHash != null && oldBlockHash != this.blockHash)
                    return Task.FromException(new InvalidOperationException("Invalid oldBlockHash"));
                this.blockHash = nextBlockHash;
                foreach (var unspent in unspentOutputs)
                {
                    UnspentOutputs existing;
                    if (this.unspents.TryGetValue(unspent.TransactionId, out existing))
                    {
                        existing.Spend(unspent);
                    }
                    else
                    {
                        existing = unspent.Clone();
                        this.unspents.Add(unspent.TransactionId, existing);
                    }
                    if (existing.IsPrunable)
                        this.unspents.Remove(unspent.TransactionId);
                }
            }
            return Task.FromResult(true);
        }

        public override Task<uint256> Rewind()
        {
            throw new NotImplementedException();
        }
    }
}
