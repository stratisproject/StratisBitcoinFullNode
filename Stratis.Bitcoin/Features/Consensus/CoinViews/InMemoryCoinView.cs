using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Coinview that holds all information in the memory, which is used in tests.
    /// </summary>
    /// <remarks>Rewinding is not supported in this implementation.</remarks>
    public class InMemoryCoinView : CoinView
    {
        /// <summary>Lock object to protect access to <see cref="unspents"/> and <see cref="blockHash"/>.</summary>
        private readonly ReaderWriterLock lockobj = new ReaderWriterLock();

        /// <summary>Information about unspent outputs mapped by transaction IDs the outputs belong to.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockobj"/>.</remarks>
        private readonly Dictionary<uint256, UnspentOutputs> unspents = new Dictionary<uint256, UnspentOutputs>();

        /// <summary>Hash of the block headers of the tip of the coinview.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockobj"/>.</remarks>
        private uint256 blockHash;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        /// <param name="blockHash">Hash of the block headers of the tip of the coinview.</param>
        public InMemoryCoinView(uint256 blockHash)
        {
            this.blockHash = blockHash;
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash)
        {
            Guard.NotNull(oldBlockHash, nameof(oldBlockHash));
            Guard.NotNull(nextBlockHash, nameof(nextBlockHash));
            Guard.NotNull(unspentOutputs, nameof(unspentOutputs));

            using (this.lockobj.LockWrite())
            {
                if ((this.blockHash != null) && (oldBlockHash != this.blockHash))
                    return Task.FromException(new InvalidOperationException("Invalid oldBlockHash"));

                this.blockHash = nextBlockHash;
                foreach (UnspentOutputs unspent in unspentOutputs)
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

        /// <inheritdoc />
        public override Task<uint256> Rewind()
        {
            throw new NotImplementedException();
        }
    }
}
