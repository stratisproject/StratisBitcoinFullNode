using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    /// <summary>
    /// Interface for interacting with the cross-chain transfer database.
    /// </summary>
    public interface ICrossChainTransferStore : IDisposable
    {
        /// <summary>
        /// Get the cross-chain transfer information from the database, identified by the deposit transaction ids.
        /// </summary>
        /// <param name="depositIds">The deposit transaction ids.</param>
        /// <returns>The cross-chain transfer information.</returns>
        Task<CrossChainTransfer[]> GetAsync(uint256[] depositIds);

        /// <summary>
        /// Records the mature deposits from <see cref="NextMatureDepositHeight"/> on the counter-chain.
        /// The value of <see cref="NextMatureDepositHeight"/> is incremented at the end of this call.
        /// The caller should check that <see cref="NextMatureDepositHeight"/> is a height on the
        /// counter-chain which would contain mature deposits.
        /// </summary>
        /// <param name="crossChainTransfers">The deposit transactions.</param>
        /// <remarks>
        /// When building the list of transfers the caller should first use <see cref="GetAsync"/>
        /// to check whether the transfer already exists without the deposit information and
        /// then provide the updated object in this call.
        /// The caller must also ensure the transfers passed to this call all have a
        /// <see cref="CrossChainTransfer.Status"/> of <see cref="CrossChainTransferStatus.Partial"/>.
        /// </remarks>
        Task RecordLatestMatureDeposits(IEnumerable<CrossChainTransfer> crossChainTransfers);

        /// <summary>
        /// Uses the information contained in our chain's blocks to update the store.
        /// Sets the <see cref="CrossChainTransferStatus.SeenInBlock"/> status for transfers
        /// identified in the blocks.
        /// </summary>
        /// <param name="newTip">The new <see cref="ChainTip"/>.</param>
        /// <param name="blocks">The blocks used to update the store. Must be sorted by ascending height leading up to the new tip.</param>
        Task PutAsync(HashHeightPair newTip, List<Block> blocks);

        /// <summary>
        /// Used in case of a reorg to revert status from <see cref="CrossChainTransferStatus.SeenInBlock"/> to
        /// <see cref="CrossChainTransferStatus.FullySigned"/>.
        /// </summary>
        /// <param name="chain">The current consensus for our chain.</param>
        Task DeleteAsync(ConcurrentChain chain);

        /// <summary>
        /// Updates partial transactions in the store with signatures obtained from the passed transactions.
        /// The <see cref="CrossChainTransferStatus.FullySigned"/> status is set on fully signed transactions.
        /// </summary>
        /// <param name="depositId">The deposit transaction to update.</param>
        /// <param name="partialTransactions">Partial transactions received from other federation members.</param>
        Task MergeTransactionSignaturesAsync(uint256 depositId, Transaction[] partialTransactions);

        /// <summary>
        /// Sets the cross-chaintransfer status associated with the rejected transaction to to <see cref="CrossChainTransferStatus.Rejected"/>.
        /// </summary>
        /// <param name="transaction">The transaction that was rejected.</param>
        Task SetRejectedStatusAsync(Transaction transaction);

        /// <summary>
        /// Returns all fully signed transactions. The caller is responsible for checking the memory pool and
        /// not re-broadcasting transactions unneccessarily.
        /// </summary>
        /// <returns>An array of fully signed transactions.</returns>
        Task<Transaction[]> GetTransactionsToBroadcastAsync();

        /// <summary>
        /// The tip of our chain when we last updated the store.
        /// </summary>
        HashHeightPair TipHashAndHeight { get; }

        /// <summary>
        /// The block height on the counter-chain for which the next list of deposits is expected.
        /// </summary>
        int NextMatureDepositHeight { get; }
    }

    public class CrossChainTransferStore : ICrossChainTransferStore
    {
        /// <summary>This table contains the cross-chain transfer information.</summary>
        private const string transferTableName = "Transfers";

        /// <summary>This table keeps track of the chain tips so that we know exactly what data our transfer table contains.</summary>
        private const string commonTableName = "Common";

        /// <summary>This contains deposits ids indexed by block hash of the corresponding transaction.</summary>
        private Dictionary<uint256, HashSet<uint256>> depositIdsByBlockHash = new Dictionary<uint256, HashSet<uint256>>();

        /// <summary>This contains the block heights by block hashes for only the blocks of interest in our chain.</summary>
        private Dictionary<uint256, int> blockHeightsByBlockHash = new Dictionary<uint256, int>();

        /// <summary>This table contains deposits ids by status.</summary>
        private Dictionary<CrossChainTransferStatus, HashSet<uint256>> depositsIdsByStatus = new Dictionary<CrossChainTransferStatus, HashSet<uint256>>();

        /// <inheritdoc />
        public int NextMatureDepositHeight { get; private set; }

        /// <inheritdoc />
        public HashHeightPair TipHashAndHeight { get; private set; }

        /// <summary>The key of the repository tip in the common table.</summary>
        private static readonly byte[] RepositoryTipKey = new byte[] { 0 };

        /// <summary>The key of the counter-chain last mature block tip in the common table.</summary>
        private static readonly byte[] NextMatureTipKey = new byte[] { 1 };

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine DBreeze;

        private readonly Network network;

        private readonly DepositExtractor depositExtractor;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        public CrossChainTransferStore(Network network, DataFolder dataFolder, FederationGatewaySettings settings, IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory, IOpReturnDataReader opReturnDataReader)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(dataFolder, nameof(dataFolder));
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.depositExtractor = new DepositExtractor(loggerFactory, settings, opReturnDataReader);
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            string folder = Path.Combine(dataFolder.RootPath, settings.IsMainChain ? "mainchaindata" : "sidechaindata");
            Directory.CreateDirectory(folder);
            this.DBreeze = new DBreezeEngine(folder);
            this.network = network;
            this.dateTimeProvider = dateTimeProvider;
            this.TipHashAndHeight = null;
            this.NextMatureDepositHeight = 0;

            // Initialize tracking deposits by status.
            foreach (var status in typeof(CrossChainTransferStatus).GetEnumValues())
                this.depositsIdsByStatus[(CrossChainTransferStatus)status] = new HashSet<uint256>();
        }

        /// <summary>Performs any needed initialisation for the database.</summary>
        public virtual Task InitializeAsync()
        {
            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
                {
                    this.LoadTipHashAndHeight(dbreezeTransaction);
                    this.LoadNextMatureHeight(dbreezeTransaction);

                    // Initialize the lookups.
                    foreach (Row<byte[], CrossChainTransfer> transferRow in dbreezeTransaction.SelectForward<byte[], CrossChainTransfer>(transferTableName))
                    {
                        CrossChainTransfer transfer = transferRow.Value;

                        this.depositsIdsByStatus[transfer.Status].Add(transfer.DepositTransactionId);

                        if (transfer.BlockHash != null)
                        {
                            if (!this.depositIdsByBlockHash.TryGetValue(transfer.BlockHash, out HashSet<uint256> deposits))
                            {
                                deposits = new HashSet<uint256>();
                            }

                            deposits.Add(transfer.DepositTransactionId);

                            this.blockHeightsByBlockHash[transfer.BlockHash] = transfer.BlockHeight;
                        }
                    }
                }

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        /// <inheritdoc />
        public Task RecordLatestMatureDeposits(IEnumerable<CrossChainTransfer> crossChainTransfers)
        {
            Guard.NotNull(crossChainTransfers, nameof(crossChainTransfers));
            Guard.Assert(!crossChainTransfers.Any(t => !t.IsValid()));
            Guard.Assert(!crossChainTransfers.Any(t => t.Status != CrossChainTransferStatus.Partial));
            Guard.Assert(!crossChainTransfers.Any(t => t.DepositBlockHeight != this.NextMatureDepositHeight));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
                {
                    dbreezeTransaction.SynchronizeTables(transferTableName, commonTableName);

                    foreach (CrossChainTransfer transfer in crossChainTransfers)
                    {
                        this.PutTransferAsync(dbreezeTransaction, transfer).GetAwaiter().GetResult();
                    }

                    // Commit additions
                    this.SaveNextMatureHeight(dbreezeTransaction, this.NextMatureDepositHeight + 1);
                    dbreezeTransaction.Commit();
                }

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        /// <inheritdoc />
        public Task MergeTransactionSignaturesAsync(uint256 depositId, Transaction[] partialTransactions)
        {
            Guard.NotNull(partialTransactions, nameof(partialTransactions));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
                {
                    dbreezeTransaction.SynchronizeTables(transferTableName, commonTableName);

                    CrossChainTransfer transfer = this.GetAsync(new[] { depositId }).GetAwaiter().GetResult().FirstOrDefault();

                    if (transfer != null)
                    {
                        transfer.CombineSignatures(this.network, partialTransactions);

                        // TODO: Update status to FullySigned when appropriate.

                        this.PutTransferAsync(dbreezeTransaction, transfer).GetAwaiter().GetResult();
                    }

                    dbreezeTransaction.Commit();
                }

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        /// <inheritdoc />
        public Task PutAsync(HashHeightPair newTip, List<Block> blocks)
        {
            Guard.NotNull(newTip, nameof(newTip));
            Guard.NotNull(blocks, nameof(blocks));
            Guard.Assert(blocks.Count == 0 || blocks[0].Header.HashPrevBlock == (this.TipHashAndHeight?.Hash ?? 0));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(transferTableName, commonTableName);
                    this.OnInsertBlocks(transaction, newTip.Height - blocks.Count + 1, blocks);

                    // Commit additions
                    this.SaveTipHashAndHeight(transaction, newTip);
                    transaction.Commit();
                }

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        /// <inheritdoc />
        public Task DeleteAsync(ConcurrentChain chain)
        {
            Guard.NotNull(chain, nameof(chain));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                // If the chain does not contain our tip..
                if (chain.GetBlock(this.TipHashAndHeight.Hash) == null)
                {
                    uint256 commonTip = this.network.GenesisHash;
                    int commonHeight = 0;

                    ChainedHeader fork = chain.FindFork(this.depositIdsByBlockHash.OrderByDescending(d => this.blockHeightsByBlockHash[d.Key]).Select(d => d.Key));

                    if (fork != null)
                    {
                        commonTip = fork.Block.GetHash();
                        commonHeight = this.blockHeightsByBlockHash[commonTip];
                    }

                    using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                    {
                        transaction.SynchronizeTables(transferTableName, commonTableName);
                        transaction.ValuesLazyLoadingIsOn = false;
                        this.OnDeleteBlocks(transaction, commonHeight);
                        this.SaveTipHashAndHeight(transaction, new HashHeightPair(commonTip, commonHeight));
                        transaction.Commit();
                    }
                }

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        /// <summary>
        /// Loads the tip and hash height.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <returns>The hash and height pair.</returns>
        private HashHeightPair LoadTipHashAndHeight(DBreeze.Transactions.Transaction dbreezeTransaction)
        {
            if (this.TipHashAndHeight == null)
            {
                dbreezeTransaction.ValuesLazyLoadingIsOn = false;

                Row<byte[], HashHeightPair> row = dbreezeTransaction.Select<byte[], HashHeightPair>(commonTableName, RepositoryTipKey);
                if (row.Exists)
                    this.TipHashAndHeight = row.Value;
            }

            return this.TipHashAndHeight;
        }

        /// <summary>
        /// Saves the tip and hash height.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="newTip">The new tip to persist.</param>
        private void SaveTipHashAndHeight(DBreeze.Transactions.Transaction dbreezeTransaction, HashHeightPair newTip)
        {
            this.TipHashAndHeight = newTip;
            dbreezeTransaction.Insert<byte[], HashHeightPair>(commonTableName, RepositoryTipKey, this.TipHashAndHeight);
        }

        /// <summary>
        /// Loads the counter-chain next mature block height.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <returns>The hash and height pair.</returns>
        private int LoadNextMatureHeight(DBreeze.Transactions.Transaction dbreezeTransaction)
        {
            if (this.TipHashAndHeight == null)
            {
                dbreezeTransaction.ValuesLazyLoadingIsOn = false;

                Row<byte[], int> row = dbreezeTransaction.Select<byte[], int>(commonTableName, NextMatureTipKey);
                if (row.Exists)
                    this.NextMatureDepositHeight = row.Value;
            }

            return this.NextMatureDepositHeight;
        }

        /// <summary>
        /// Saves the counter-chain next mature block height.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="newTip">The next mature block height on the counter-chain.</param>
        private void SaveNextMatureHeight(DBreeze.Transactions.Transaction dbreezeTransaction, int newTip)
        {
            this.NextMatureDepositHeight = newTip;
            dbreezeTransaction.Insert<byte[], int>(commonTableName, NextMatureTipKey, this.NextMatureDepositHeight);
        }

        /// <inheritdoc />
        public Task<CrossChainTransfer[]> GetAsync(uint256[] depositId)
        {
            Guard.NotNull(depositId, nameof(depositId));

            Task<CrossChainTransfer[]> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                // To boost performance we will access the deposits sorted by deposit id.
                var depositDict = new Dictionary<uint256, int>();
                for (int i = 0; i < depositId.Length; i++)
                    depositDict[depositId[i]] = i;

                var byteListComparer = new ByteListComparer();
                List<KeyValuePair<uint256, int>> depositList = depositDict.ToList();
                depositList.Sort((pair1, pair2) => byteListComparer.Compare(pair1.Key.ToBytes(), pair2.Key.ToBytes()));

                var res = new CrossChainTransfer[depositId.Length];
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    foreach (KeyValuePair<uint256, int> kv in depositList)
                    {
                        Row<byte[], CrossChainTransfer> transferRow = transaction.Select<byte[], CrossChainTransfer>(transferTableName, kv.Key.ToBytes());

                        if (transferRow.Exists)
                        {
                            res[kv.Value] = transferRow.Value;
                        }
                    }
                }

                this.logger.LogTrace("(-):{0}", res);
                return res;
            });

            return task;
        }

        /// <inheritdoc />
        public Task<Transaction[]> GetTransactionsToBroadcastAsync()
        {
            Task<Transaction[]> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                uint256[] fullySignedTransfers = this.depositsIdsByStatus[CrossChainTransferStatus.FullySigned].ToArray();

                CrossChainTransfer[] transfers = this.GetAsync(fullySignedTransfers).GetAwaiter().GetResult();

                Transaction[] res = transfers.Select(t => t.PartialTransaction).ToArray();

                this.logger.LogTrace("(-){0}", res);

                return res;
            });

            return task;
        }

        /// <inheritdoc />
        public Task SetRejectedStatusAsync(Transaction transaction)
        {
            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                IDeposit deposit = this.depositExtractor.ExtractDepositFromTransaction(transaction, 0, 0);
                if (deposit == null)
                {
                    this.logger.LogTrace("(-)[NO_DEPOSIT]");
                    return;
                }

                CrossChainTransfer crossChainTransfer = this.GetAsync(new[] { deposit.Id }).GetAwaiter().GetResult().FirstOrDefault();
                if (crossChainTransfer == null)
                {
                    this.logger.LogTrace("(-)[NO_TRANSFER]");
                    return;
                }

                using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
                {
                    dbreezeTransaction.SynchronizeTables(transferTableName, commonTableName);
                    dbreezeTransaction.ValuesLazyLoadingIsOn = false;
                    crossChainTransfer.SetStatus(CrossChainTransferStatus.Rejected);
                    this.PutTransferAsync(dbreezeTransaction, crossChainTransfer).GetAwaiter().GetResult();
                    dbreezeTransaction.Commit();
                }

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        /// <summary>
        /// Persist the cross-chain transfer information into the database.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="crossChainTransfer">Cross-chain transfer information to be inserted.</param>
        private Task PutTransferAsync(DBreeze.Transactions.Transaction dbreezeTransaction, CrossChainTransfer crossChainTransfer)
        {
            Guard.NotNull(crossChainTransfer, nameof(crossChainTransfer));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                dbreezeTransaction.Insert<byte[], CrossChainTransfer>(transferTableName, crossChainTransfer.DepositTransactionId.ToBytes(), crossChainTransfer);

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        /// <summary>
        /// Records transfer information from the supplied blocks.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="blockHeight">The block height of the first block in the list.</param>
        /// <param name="blocks">The list of blocks to add.</param>
        private void OnInsertBlocks(DBreeze.Transactions.Transaction dbreezeTransaction, int blockHeight, List<Block> blocks)
        {
            // Find transfer transactions in blocks
            foreach (Block block in blocks)
            {
                IReadOnlyList<IDeposit> deposits = this.depositExtractor.ExtractDepositsFromBlock(block, blockHeight);

                // First check the database to see if we already know about these deposits.
                CrossChainTransfer[] storedDeposits = this.GetAsync(deposits.Select(d => d.Id).ToArray()).GetAwaiter().GetResult();

                // Update the information about these deposits or record their status.
                for (int i = 0; i < storedDeposits.Length; i++)
                {
                    IDeposit deposit = deposits[i];

                    if (storedDeposits[i] == null)
                    {
                        Script scriptPubKey = BitcoinAddress.Create(deposit.TargetAddress, this.network).ScriptPubKey;
                        Transaction transaction = block.Transactions.Single(t => t.GetHash() == deposit.Id);

                        storedDeposits[i] = new CrossChainTransfer(CrossChainTransferStatus.SeenInBlock, deposit.Id, -1 /* Unknown */,
                            scriptPubKey, deposit.Amount, transaction, block.GetHash(), blockHeight);

                        // Update the lookups.
                        this.depositsIdsByStatus[CrossChainTransferStatus.SeenInBlock].Add(storedDeposits[i].DepositTransactionId);
                        this.depositIdsByBlockHash[block.GetHash()].Add(deposit.Id);
                    }
                    else
                    {
                        // Update the lookups.
                        this.SetTransferStatus(storedDeposits[i], CrossChainTransferStatus.SeenInBlock);
                    }

                    this.PutTransferAsync(dbreezeTransaction, storedDeposits[i]).GetAwaiter().GetResult();
                }

                // Update lookups.
                this.blockHeightsByBlockHash[block.GetHash()] = blockHeight++;
            }
        }

        /// <summary>
        /// Forgets transfer information from the blocks being removed.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="lastBlockHeight">The last block to retain.</param>
        private void OnDeleteBlocks(DBreeze.Transactions.Transaction dbreezeTransaction, int lastBlockHeight)
        {
            // Gather all the deposit ids.
            var depositIds = new HashSet<uint256>();
            uint256[] blocksToRemove = this.blockHeightsByBlockHash.Where(a => a.Value > lastBlockHeight).Select(a => a.Key).ToArray();

            foreach (HashSet<uint256> deposits in blocksToRemove.Select(a => this.depositIdsByBlockHash[a]))
            {
                depositIds.UnionWith(deposits);
            }

            foreach (KeyValuePair<uint256, HashSet<uint256>> kv in this.depositIdsByBlockHash)
            {
                int blockHeight = this.blockHeightsByBlockHash[kv.Key];
                if (blockHeight > lastBlockHeight)
                {
                    depositIds.UnionWith(kv.Value);
                }
            }

            // First check the database to see if we already know about these deposits.
            CrossChainTransfer[] crossChainTransfers = this.GetAsync(depositIds.ToArray()).GetAwaiter().GetResult();

            foreach (CrossChainTransfer transfer in crossChainTransfers)
            {
                // Transaction is no longer seen.
                this.SetTransferStatus(transfer, CrossChainTransferStatus.FullySigned);

                // Write the transfer status to the database.
                this.PutTransferAsync(dbreezeTransaction, transfer).GetAwaiter().GetResult();

                // Update the lookups.
                this.depositIdsByBlockHash[transfer.BlockHash].Remove(transfer.DepositTransactionId);
            }

            // Update the lookups.
            foreach (uint256 blockHash in blocksToRemove)
            {
                this.blockHeightsByBlockHash.Remove(blockHash);
            }
        }

        /// <summary>
        /// Updates the status of the transfer and the status lookup.
        /// </summary>
        /// <param name="transfer">The cross-chain transfer to update.</param>
        /// <param name="status">The new status.</param>
        private void SetTransferStatus(CrossChainTransfer transfer, CrossChainTransferStatus status)
        {
            this.depositsIdsByStatus[transfer.Status].Remove(transfer.DepositTransactionId);
            transfer.SetStatus(status);
            this.depositsIdsByStatus[transfer.Status].Add(transfer.DepositTransactionId);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.DBreeze.Dispose();
        }
    }
}
