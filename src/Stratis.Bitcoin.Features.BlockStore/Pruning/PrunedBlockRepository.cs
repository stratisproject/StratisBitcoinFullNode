using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore.Pruning
{
    /// <inheritdoc />
    public class PrunedBlockRepository : IPrunedBlockRepository
    {
        private readonly IBlockRepository blockRepository;
        private readonly DBreezeSerializer dBreezeSerializer;
        private readonly ILogger logger;
        private static readonly byte[] prunedTipKey = new byte[2];
        private readonly StoreSettings storeSettings;

        /// <inheritdoc />
        public HashHeightPair PrunedTip { get; private set; }

        public PrunedBlockRepository(IBlockRepository blockRepository, DBreezeSerializer dBreezeSerializer, ILoggerFactory loggerFactory, StoreSettings storeSettings)
        {
            this.blockRepository = blockRepository;
            this.dBreezeSerializer = dBreezeSerializer;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.storeSettings = storeSettings;
        }

        /// <inheritdoc />
        public Task InitializeAsync()
        {
            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.blockRepository.DBreeze.GetTransaction())
                {
                    this.LoadPrunedTip(transaction);
                }
            });

            return task;
        }

        /// <inheritdoc />
        public async Task PruneAndCompactDatabase(ChainedHeader blockRepositoryTip, Network network, bool nodeInitializing)
        {
            this.logger.LogInformation($"Pruning started.");

            if (this.PrunedTip == null)
            {
                Block genesis = network.GetGenesis();

                this.PrunedTip = new HashHeightPair(genesis.GetHash(), 0);

                using (DBreeze.Transactions.Transaction transaction = this.blockRepository.DBreeze.GetTransaction())
                {
                    transaction.Insert(BlockRepository.CommonTableName, prunedTipKey, this.dBreezeSerializer.Serialize(this.PrunedTip));
                    transaction.Commit();
                }
            }

            if (nodeInitializing)
            {
                if (IsDatabasePruned())
                    return;

                await this.PrepareDatabaseForCompactingAsync(blockRepositoryTip).ConfigureAwait(false);
            }

            this.CompactDataBase();

            this.logger.LogInformation($"Pruning complete.");

            return;
        }

        private bool IsDatabasePruned()
        {
            if (this.blockRepository.TipHashAndHeight.Height <= this.PrunedTip.Height + this.storeSettings.AmountOfBlocksToKeep)
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
        private async Task PrepareDatabaseForCompactingAsync(ChainedHeader blockRepositoryTip)
        {
            int upperHeight = this.blockRepository.TipHashAndHeight.Height - this.storeSettings.AmountOfBlocksToKeep;

            var toDelete = new List<ChainedHeader>();

            ChainedHeader startFromHeader = blockRepositoryTip.GetAncestor(upperHeight);
            ChainedHeader endAtHeader = blockRepositoryTip.FindAncestorOrSelf(this.PrunedTip.Hash);

            this.logger.LogInformation($"Pruning blocks from height {upperHeight} to {endAtHeader.Height}.");

            while (startFromHeader.Previous != null && startFromHeader != endAtHeader)
            {
                toDelete.Add(startFromHeader);
                startFromHeader = startFromHeader.Previous;
            }

            await this.blockRepository.DeleteBlocksAsync(toDelete.Select(cb => cb.HashBlock).ToList()).ConfigureAwait(false);

            this.UpdatePrunedTip(blockRepositoryTip.GetAncestor(upperHeight));
        }

        private void LoadPrunedTip(DBreeze.Transactions.Transaction dbreezeTransaction)
        {
            if (this.PrunedTip == null)
            {
                dbreezeTransaction.ValuesLazyLoadingIsOn = false;

                Row<byte[], byte[]> row = dbreezeTransaction.Select<byte[], byte[]>(BlockRepository.CommonTableName, prunedTipKey);
                if (row.Exists)
                    this.PrunedTip = this.dBreezeSerializer.Deserialize<HashHeightPair>(row.Value);

                dbreezeTransaction.ValuesLazyLoadingIsOn = true;
            }
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

                    var tempBlocks = dbreezeTransaction.SelectDictionary<byte[], byte[]>(BlockRepository.BlockTableName);

                    if (tempBlocks.Count != 0)
                    {
                        this.logger.LogInformation($"{tempBlocks.Count} blocks will be copied to the pruned table.");

                        dbreezeTransaction.RemoveAllKeys(BlockRepository.BlockTableName, true);
                        dbreezeTransaction.InsertDictionary(BlockRepository.BlockTableName, tempBlocks, false);

                        var tempTransactions = dbreezeTransaction.SelectDictionary<byte[], byte[]>(BlockRepository.TransactionTableName);
                        if (tempTransactions.Count != 0)
                        {
                            this.logger.LogInformation($"{tempTransactions.Count} transactions will be copied to the pruned table.");
                            dbreezeTransaction.RemoveAllKeys(BlockRepository.TransactionTableName, true);
                            dbreezeTransaction.InsertDictionary(BlockRepository.TransactionTableName, tempTransactions, false);
                        }

                        // Save the hash and height of where the node was pruned up to.
                        dbreezeTransaction.Insert(BlockRepository.CommonTableName, prunedTipKey, this.dBreezeSerializer.Serialize(this.PrunedTip));
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
