using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.DB;

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
                using (IStratisDBTransaction dbTransaction = this.blockRepository.StratisDB.CreateTransaction(StratisDBTransactionMode.Read))
                {
                    this.LoadPrunedTip(dbTransaction);
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

                using (IStratisDBTransaction dbTransaction = this.blockRepository.StratisDB.CreateTransaction(StratisDBTransactionMode.ReadWrite))
                {
                    dbTransaction.Insert(BlockRepository.CommonTableName, prunedTipKey, this.PrunedTip);
                    dbTransaction.Commit();
                }
            }

            if (nodeInitializing)
            {
                if (this.IsDatabasePruned())
                    return;

                this.PrepareDatabaseForCompacting(blockRepositoryTip);
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
        private void PrepareDatabaseForCompacting(ChainedHeader blockRepositoryTip)
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

            this.blockRepository.DeleteBlocks(toDelete.Select(cb => cb.HashBlock).ToList());

            this.UpdatePrunedTip(blockRepositoryTip.GetAncestor(upperHeight));
        }

        private void LoadPrunedTip(IStratisDBTransaction dbTransaction)
        {
            if (this.PrunedTip == null)
            {
                if (dbTransaction.Select(BlockRepository.CommonTableName, prunedTipKey, out HashHeightPair prunedTip))
                    this.PrunedTip = prunedTip;
            }
        }

        /// <summary>
        /// Compacts the block and transaction database by recreating the tables without the deleted references.
        /// </summary>
        private void CompactDataBase()
        {
            Task task = Task.Run(() =>
            {
                using (IStratisDBTransaction dbTransaction = this.blockRepository.StratisDB.CreateTransaction(StratisDBTransactionMode.ReadWrite, BlockRepository.BlockTableName, BlockRepository.TransactionTableName, BlockRepository.CommonTableName))
                {
                    var tempBlocks = dbTransaction.SelectDictionary<byte[], byte[]>(BlockRepository.BlockTableName);

                    if (tempBlocks.Count != 0)
                    {
                        this.logger.LogInformation($"{tempBlocks.Count} blocks will be copied to the pruned table.");

                        dbTransaction.RemoveAllKeys(BlockRepository.BlockTableName);
                        dbTransaction.InsertDictionary(BlockRepository.BlockTableName, tempBlocks);

                        var tempTransactions = dbTransaction.SelectDictionary<byte[], byte[]>(BlockRepository.TransactionTableName);
                        if (tempTransactions.Count != 0)
                        {
                            this.logger.LogInformation($"{tempTransactions.Count} transactions will be copied to the pruned table.");
                            dbTransaction.RemoveAllKeys(BlockRepository.TransactionTableName);
                            dbTransaction.InsertDictionary(BlockRepository.TransactionTableName, tempTransactions);
                        }

                        // Save the hash and height of where the node was pruned up to.
                        dbTransaction.Insert(BlockRepository.CommonTableName, prunedTipKey, this.PrunedTip);
                    }

                    dbTransaction.Commit();
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
