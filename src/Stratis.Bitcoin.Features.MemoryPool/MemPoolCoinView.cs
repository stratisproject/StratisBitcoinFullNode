using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Memory pool coin view.
    /// Provides coin view representation of memory pool transactions via a backed coin view.
    /// </summary>
    public class MempoolCoinView : ICoinView, IBackedCoinView
    {
        /// <summary>Transaction memory pool for managing transactions in the memory pool.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="mempoolLock"/>.</remarks>
        private readonly ITxMempool memPool;

        /// <summary>A lock for protecting access to <see cref="memPool"/>.</summary>
        private readonly SchedulerLock mempoolLock;

        /// <summary>Memory pool validator for validating transactions.</summary>
        private readonly IMempoolValidator mempoolValidator;

        /// <summary>
        /// Constructs a memory pool coin view.
        /// </summary>
        /// <param name="inner">The backing coin view.</param>
        /// <param name="memPool">Transaction memory pool for managing transactions in the memory pool.</param>
        /// <param name="mempoolLock">A lock for managing asynchronous access to memory pool.</param>
        /// <param name="mempoolValidator">Memory pool validator for validating transactions.</param>
        public MempoolCoinView(ICoinView inner, ITxMempool memPool, SchedulerLock mempoolLock, IMempoolValidator mempoolValidator)
        {
            this.Inner = inner;
            this.memPool = memPool;
            this.mempoolLock = mempoolLock;
            this.mempoolValidator = mempoolValidator;
            this.Set = new UnspentOutputSet();
        }

        /// <summary>
        /// Gets the unspent transaction output set.
        /// </summary>
        public UnspentOutputSet Set { get; private set; }

        /// <summary>
        /// Backing coin view instance.
        /// </summary>
        public ICoinView Inner { get; }

        /// <inheritdoc />
        public Task SaveChangesAsync(IList<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash,
            uint256 nextBlockHash, int height, List<RewindData> rewindDataList = null)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<uint256> GetTipHashAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<uint256> RewindAsync()
        {
            throw new NotImplementedException();
        }

        public Task<RewindData> GetRewindData(int height)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Load the coin view for a memory pool transaction.
        /// </summary>
        /// <param name="trx">Memory pool transaction.</param>
        public async Task LoadViewAsync(Transaction trx)
        {
            // lookup all ids (duplicate ids are ignored in case a trx spends outputs from the same parent).
            List<uint256> ids = trx.Inputs.Select(n => n.PrevOut.Hash).Distinct().Concat(new[] { trx.GetHash() }).ToList();
            FetchCoinsResponse coins = await this.Inner.FetchCoinsAsync(ids.ToArray());
            // find coins currently in the mempool
            List<Transaction> mempoolcoins = await this.mempoolLock.ReadAsync(() =>
            {
                return this.memPool.MapTx.Values.Where(t => ids.Contains(t.TransactionHash)).Select(s => s.Transaction).ToList();
            });
            IEnumerable<UnspentOutputs> memOutputs = mempoolcoins.Select(s => new UnspentOutputs(TxMempool.MempoolHeight, s));
            coins = new FetchCoinsResponse(coins.UnspentOutputs.Concat(memOutputs).ToArray(), coins.BlockHash);

            // the UTXO set might have been updated with a recently received block
            // but the block has not yet arrived to the mempool and remove the pending trx
            // from the pool (a race condition), block validation doesn't lock the mempool.
            // its safe to ignore duplicats on the UTXO set as duplicates mean a trx is in
            // a block and the block will soon remove the trx from the pool.
            this.Set.TrySetCoins(coins.UnspentOutputs);
        }

        /// <summary>
        /// Gets the unspent outputs for a given transaction id.
        /// </summary>
        /// <param name="txid">Transaction identifier.</param>
        /// <returns>The unspent outputs.</returns>
        public UnspentOutputs GetCoins(uint256 txid)
        {
            return this.Set.AccessCoins(txid);
        }

        /// <summary>
        /// Check whether a transaction id exists in the <see cref="TxMempool"/> or in the <see cref="MempoolCoinView"/>.
        /// </summary>
        /// <param name="txid">Transaction identifier.</param>
        /// <returns>Whether coins exist.</returns>
        public bool HaveCoins(uint256 txid)
        {
            if (this.memPool.Exists(txid))
                return true;

            return this.Set.AccessCoins(txid) != null;
        }

        /// <summary>
        /// Gets the priority of this memory pool transaction based upon chain height.
        /// </summary>
        /// <param name="tx">Memory pool transaction.</param>
        /// <param name="nHeight">Chain height.</param>
        /// <returns>Tuple of priority value and sum of all txin values that are already in blockchain.</returns>
        public (double priority, Money inChainInputValue) GetPriority(Transaction tx, int nHeight)
        {
            Money inChainInputValue = 0;
            if (tx.IsCoinBase)
                return (0.0, inChainInputValue);
            double dResult = 0.0;
            foreach (TxIn txInput in tx.Inputs)
            {
                UnspentOutputs coins = this.Set.AccessCoins(txInput.PrevOut.Hash);
                Guard.Assert(coins != null);
                if (!coins.IsAvailable(txInput.PrevOut.N)) continue;
                if (coins.Height <= nHeight)
                {
                    dResult += (double)coins.Outputs[txInput.PrevOut.N].Value.Satoshi * (nHeight - coins.Height);
                    inChainInputValue += coins.Outputs[txInput.PrevOut.N].Value;
                }
            }
            return (this.ComputePriority(tx, dResult), inChainInputValue);
        }

        /// <summary>
        /// Calculates the priority of a transaction based upon transaction size and priority inputs.
        /// </summary>
        /// <param name="trx">Memory pool transaction.</param>
        /// <param name="dPriorityInputs">Priority weighting of inputs.</param>
        /// <param name="nTxSize">Transaction size, 0 will compute.</param>
        /// <returns>Priority value.</returns>
        private double ComputePriority(Transaction trx, double dPriorityInputs, int nTxSize = 0)
        {
            nTxSize = MempoolValidator.CalculateModifiedSize(nTxSize, trx, this.mempoolValidator.ConsensusOptions);
            if (nTxSize == 0) return 0.0;

            return dPriorityInputs / nTxSize;
        }

        /// <summary>
        /// Whether memory pool transaction spends coin base.
        /// </summary>
        /// <param name="tx">Memory pool transaction.</param>
        /// <returns>Whether the transactions spends coin base.</returns>
        public bool SpendsCoinBase(Transaction tx)
        {
            foreach (TxIn txInput in tx.Inputs)
            {
                UnspentOutputs coins = this.Set.AccessCoins(txInput.PrevOut.Hash);
                if (coins.IsCoinbase)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether the transaction has inputs.
        /// </summary>
        /// <param name="tx">Memory pool transaction.</param>
        /// <returns>Whether the transaction has inputs.</returns>
        public bool HaveInputs(Transaction tx)
        {
            return this.Set.HaveInputs(tx);
        }

        /// <summary>
        /// Gets the value of the inputs for a memory pool transaction.
        /// </summary>
        /// <param name="tx">Memory pool transaction.</param>
        /// <returns>Value of the transaction's inputs.</returns>
        public Money GetValueIn(Transaction tx)
        {
            return this.Set.GetValueIn(tx);
        }

        /// <summary>
        /// Gets the transaction output for a transaction input.
        /// </summary>
        /// <param name="input">Transaction input.</param>
        /// <returns>Transaction output.</returns>
        public TxOut GetOutputFor(TxIn input)
        {
            return this.Set.GetOutputFor(input);
        }
    }
}
