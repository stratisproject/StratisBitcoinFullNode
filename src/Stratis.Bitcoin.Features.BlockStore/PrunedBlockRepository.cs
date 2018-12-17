using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <inheritdoc />
    public class PrunedBlockRepository : IPrunedBlockRepository
    {
        private readonly IBlockRepository blockRepository;
        private readonly ILogger logger;
        private static readonly byte[] prunedTipKey = new byte[2];
        private readonly StoreSettings storeSettings;

        /// <inheritdoc />
        public HashHeightPair PrunedTip { get; private set; }

        public PrunedBlockRepository(IBlockRepository blockRepository, ILoggerFactory loggerFactory, StoreSettings storeSettings)
        {
            this.blockRepository = blockRepository;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.storeSettings = storeSettings;
        }

        /// <inheritdoc />
        public Task InitializeAsync(Network network)
        {
            Block genesis = network.GetGenesis();

            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.blockRepository.DBreeze.GetTransaction())
                {
                    bool doCommit = false;

                    if (this.storeSettings.Prune != 0)
                    {
                        if (this.LoadPrunedTip(transaction) == null)
                        {
                            this.PrunedTip = new HashHeightPair(genesis.GetHash(), 0);
                            transaction.Insert(BlockRepository.CommonTableName, prunedTipKey, this.PrunedTip);
                            doCommit = true;
                        }
                    }

                    if (doCommit) transaction.Commit();
                }
            });

            return task;
        }

        /// <inheritdoc />
        public async Task PruneDatabase(ChainedHeader blockRepositoryTip, bool nodeInitializing)
        {
            this.logger.LogInformation($"Pruning started.");

            if (nodeInitializing)
            {
                if (IsDatabasePruned())
                    return;

                await this.PrepareDatabaseForCompactingAsync(blockRepositoryTip);
            }

            this.CompactDataBase();

            this.logger.LogInformation($"Pruning complete.");

            return;
        }

        private bool IsDatabasePruned()
        {
            if (this.blockRepository.TipHashAndHeight.Height <= this.PrunedTip.Height + this.storeSettings.Prune)
            {
                this.logger.LogDebug("(-):true");
                return true;
            }
            else
            {
                this.logger.LogDebug("(-):false");
                return false;
            }
        }

        /// <summary>
        /// Compacts the block and transaction database by recreating the tables without the deleted references.
        /// </summary>
        /// <param name="blockRepositoryTip">The last fully validated block of the node.</param>
        /// <returns>The awaited task.</returns>
        private async Task PrepareDatabaseForCompactingAsync(ChainedHeader blockRepositoryTip)
        {
            int upperHeight = this.blockRepository.TipHashAndHeight.Height - this.storeSettings.Prune;

            var toDelete = new List<ChainedHeader>();

            ChainedHeader startFromHeader = blockRepositoryTip.FindAncestorOrSelf(this.blockRepository.TipHashAndHeight.Hash);
            ChainedHeader endAtHeader = blockRepositoryTip.FindAncestorOrSelf(this.PrunedTip.Hash);

            this.logger.LogInformation($"Pruning blocks from height {upperHeight} to {endAtHeader.Height}.");

            while (startFromHeader.Previous != null && startFromHeader != endAtHeader)
            {
                toDelete.Add(startFromHeader);
                startFromHeader = startFromHeader.Previous;
            }

            await this.blockRepository.DeleteBlocksAsync(toDelete.Select(cb => cb.HashBlock).ToList()).ConfigureAwait(false);

            this.UpdatePrunedTip(blockRepositoryTip.FindAncestorOrSelf(this.blockRepository.TipHashAndHeight.Hash));
        }

        private HashHeightPair LoadPrunedTip(DBreeze.Transactions.Transaction dbreezeTransaction)
        {
            if (this.PrunedTip == null)
            {
                dbreezeTransaction.ValuesLazyLoadingIsOn = false;

                Row<byte[], HashHeightPair> row = dbreezeTransaction.Select<byte[], HashHeightPair>(BlockRepository.CommonTableName, prunedTipKey);
                if (row.Exists)
                    this.PrunedTip = row.Value;

                dbreezeTransaction.ValuesLazyLoadingIsOn = true;
            }

            return this.PrunedTip;
        }

        /// <summary>
        /// Compacts the block and transaction database by recreating the tables without the deleted references.
        /// </summary>
        private void CompactDataBase()
        {
            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction dbreezeTransaction = this.blockRepository.DBreeze.GetTransaction())
                {
                    dbreezeTransaction.SynchronizeTables(BlockRepository.BlockTableName, BlockRepository.TransactionTableName);

                    var tempBlocks = dbreezeTransaction.SelectDictionary<byte[], Block>(BlockRepository.BlockTableName);

                    if (tempBlocks.Count != 0)
                    {
                        this.logger.LogInformation($"{tempBlocks.Count} blocks will be copied to the pruned table.");

                        dbreezeTransaction.RemoveAllKeys(BlockRepository.BlockTableName, true);
                        dbreezeTransaction.InsertDictionary(BlockRepository.BlockTableName, tempBlocks, false);

                        var tempTransactions = dbreezeTransaction.SelectDictionary<byte[], Block>(BlockRepository.TransactionTableName);
                        if (tempTransactions.Count != 0)
                        {
                            this.logger.LogInformation($"{tempTransactions.Count} transactions will be copied to the pruned table.");
                            dbreezeTransaction.RemoveAllKeys(BlockRepository.TransactionTableName, true);
                            dbreezeTransaction.InsertDictionary(BlockRepository.TransactionTableName, tempTransactions, false);
                        }

                        // Save the hash and height of where the node was pruned up to.
                        dbreezeTransaction.Insert(BlockRepository.CommonTableName, prunedTipKey, this.PrunedTip);
                    }

                    dbreezeTransaction.Commit();
                }

                return Task.CompletedTask;
            });
        }

        /// <inheritdoc />
        public void UpdatePrunedTip(ChainedHeader tip)
        {
            this.PrunedTip = new HashHeightPair(tip);
        }
    }
}
