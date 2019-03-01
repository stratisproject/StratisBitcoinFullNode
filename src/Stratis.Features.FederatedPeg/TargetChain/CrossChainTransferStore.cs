using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.Wallet;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    public class CrossChainTransferStore : ICrossChainTransferStore
    {
        /// <summary>This table contains the cross-chain transfer information.</summary>
        private const string transferTableName = "Transfers";

        /// <summary>This table keeps track of the chain tips so that we know exactly what data our transfer table contains.</summary>
        private const string commonTableName = "Common";

        // <summary>Block batch size for synchronization</summary>
        private const int synchronizationBatchSize = 1000;

        /// <summary>This contains deposits ids indexed by block hash of the corresponding transaction.</summary>
        private readonly Dictionary<uint256, HashSet<uint256>> depositIdsByBlockHash = new Dictionary<uint256, HashSet<uint256>>();

        /// <summary>This contains the block heights by block hashes for only the blocks of interest in our chain.</summary>
        private readonly Dictionary<uint256, int> blockHeightsByBlockHash = new Dictionary<uint256, int>();

        /// <summary>This table contains deposits ids by status.</summary>
        private readonly Dictionary<CrossChainTransferStatus, HashSet<uint256>> depositsIdsByStatus = new Dictionary<CrossChainTransferStatus, HashSet<uint256>>();

        /// <inheritdoc />
        public int NextMatureDepositHeight { get; private set; }

        /// <inheritdoc />
        public ChainedHeader TipHashAndHeight { get; private set; }

        /// <summary>The key of the repository tip in the common table.</summary>
        private static readonly byte[] RepositoryTipKey = new byte[] { 0 };

        /// <summary>The key of the counter-chain last mature block tip in the common table.</summary>
        private static readonly byte[] NextMatureTipKey = new byte[] { 1 };

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine DBreeze;

        private readonly DBreezeSerializer dBreezeSerializer;
        private readonly Network network;
        private readonly ConcurrentChain chain;
        private readonly IWithdrawalExtractor withdrawalExtractor;
        private readonly IBlockRepository blockRepository;
        private readonly CancellationTokenSource cancellation;
        private readonly IFederationWalletManager federationWalletManager;
        private readonly IWithdrawalTransactionBuilder withdrawalTransactionBuilder;

        /// <summary>Provider of time functions.</summary>
        private readonly object lockObj;

        public CrossChainTransferStore(Network network, DataFolder dataFolder, ConcurrentChain chain, IFederationGatewaySettings settings, IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory, IWithdrawalExtractor withdrawalExtractor, IFullNode fullNode, IBlockRepository blockRepository,
            IFederationWalletManager federationWalletManager, IWithdrawalTransactionBuilder withdrawalTransactionBuilder, DBreezeSerializer dBreezeSerializer)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(dataFolder, nameof(dataFolder));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(withdrawalExtractor, nameof(withdrawalExtractor));
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(blockRepository, nameof(blockRepository));
            Guard.NotNull(federationWalletManager, nameof(federationWalletManager));
            Guard.NotNull(withdrawalTransactionBuilder, nameof(withdrawalTransactionBuilder));

            this.network = network;
            this.chain = chain;
            this.blockRepository = blockRepository;
            this.federationWalletManager = federationWalletManager;
            this.withdrawalTransactionBuilder = withdrawalTransactionBuilder;
            this.withdrawalExtractor = withdrawalExtractor;
            this.dBreezeSerializer = dBreezeSerializer;
            this.lockObj = new object();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.TipHashAndHeight = this.chain.GetBlock(0);
            this.NextMatureDepositHeight = 1;
            this.cancellation = new CancellationTokenSource();

            // Future-proof store name.
            string depositStoreName = "federatedTransfers" + settings.MultiSigAddress.ToString();
            string folder = Path.Combine(dataFolder.RootPath, depositStoreName);
            Directory.CreateDirectory(folder);
            this.DBreeze = new DBreezeEngine(folder);

            // Initialize tracking deposits by status.
            foreach (object status in typeof(CrossChainTransferStatus).GetEnumValues())
                this.depositsIdsByStatus[(CrossChainTransferStatus)status] = new HashSet<uint256>();
        }

        /// <summary>Performs any needed initialisation for the database.</summary>
        public void Initialize()
        {
            lock (this.lockObj)
            {
                using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
                {
                    dbreezeTransaction.ValuesLazyLoadingIsOn = false;

                    this.LoadTipHashAndHeight(dbreezeTransaction);
                    this.LoadNextMatureHeight(dbreezeTransaction);

                    // Initialize the lookups.
                    foreach (Row<byte[], byte[]> transferRow in dbreezeTransaction.SelectForward<byte[], byte[]>(transferTableName))
                    {
                        var transfer = new CrossChainTransfer();
                        transfer.FromBytes(transferRow.Value, this.network.Consensus.ConsensusFactory);
                        this.depositsIdsByStatus[transfer.Status].Add(transfer.DepositTransactionId);

                        if (transfer.BlockHash != null && transfer.BlockHeight != null)
                        {
                            if (!this.depositIdsByBlockHash.TryGetValue(transfer.BlockHash, out HashSet<uint256> deposits))
                            {
                                deposits = new HashSet<uint256>();
                                this.depositIdsByBlockHash[transfer.BlockHash] = deposits;
                            }

                            deposits.Add(transfer.DepositTransactionId);

                            this.blockHeightsByBlockHash[transfer.BlockHash] = (int)transfer.BlockHeight;
                        }
                    }
                }
            }
        }

        /// <summary>Starts the cross-chain-transfer store.</summary>
        public void Start()
        {
            lock (this.lockObj)
            {
                // Remove all transient transactions from the wallet to be re-added according to the
                // information carried in the store. This ensures that we will re-sync in the case
                // where the store may have been deleted.
                // Any partial transfers affected by these removals are expected to first become
                // suspended due to the missing wallet transactions which will rewind the counter-
                // chain tip to then reprocess them.
                if (this.federationWalletManager.RemoveTransientTransactions())
                    this.federationWalletManager.SaveWallet();

                Guard.Assert(this.Synchronize());

                // Any transactions seen in blocks must also be present in the wallet.
                FederationWallet wallet = this.federationWalletManager.GetWallet();
                ICrossChainTransfer[] transfers = this.GetTransfersByStatus(new[] { CrossChainTransferStatus.SeenInBlock }, true, false).ToArray();
                foreach (ICrossChainTransfer transfer in transfers)
                {
                    (Transaction tran, TransactionData tranData, _) = this.federationWalletManager.FindWithdrawalTransactions(transfer.DepositTransactionId).FirstOrDefault();
                    if (tran == null && wallet.LastBlockSyncedHeight >= transfer.BlockHeight)
                    {
                        this.federationWalletManager.ProcessTransaction(transfer.PartialTransaction);
                        (tran, tranData, _) = this.federationWalletManager.FindWithdrawalTransactions(transfer.DepositTransactionId).FirstOrDefault();
                        tranData.BlockHeight = transfer.BlockHeight;
                        tranData.BlockHash = transfer.BlockHash;
                    }
                }
            }
        }

        /// <inheritdoc />
        public bool HasSuspended()
        {
            return this.depositsIdsByStatus[CrossChainTransferStatus.Suspended].Count != 0;
        }

        /// <summary>
        /// The store will chase the wallet tip. This will ensure that we can rely on
        /// information recorded in the wallet such as the list of unspent UTXO's.
        /// </summary>
        /// <returns>The height to which the wallet has been synced.</returns>
        private HashHeightPair TipToChase()
        {
            FederationWallet wallet = this.federationWalletManager.GetWallet();

            if (wallet?.LastBlockSyncedHeight == null)
            {
                this.logger.LogTrace("(-)[GENESIS]");
                return new HashHeightPair(this.network.GenesisHash, 0);
            }

            return new HashHeightPair(wallet.LastBlockSyncedHash, (int)wallet.LastBlockSyncedHeight);
        }

        /// <summary>
        /// Partial or fully signed transfers should have their source UTXO's recorded by an up-to-date wallet.
        /// Sets transfers to <see cref="CrossChainTransferStatus.Suspended"/> if their UTXO's are not reserved
        /// within the wallet.
        /// </summary>
        /// <param name="crossChainTransfers">The transfers to check. If not supplied then all partial and fully signed transfers are checked.</param>
        /// <returns>Returns the list of transfers, possible with updated statuses.</returns>
        private ICrossChainTransfer[] ValidateCrossChainTransfers(ICrossChainTransfer[] crossChainTransfers = null)
        {
            if (crossChainTransfers == null)
            {
                crossChainTransfers = Get(
                    this.depositsIdsByStatus[CrossChainTransferStatus.Partial].Union(
                        this.depositsIdsByStatus[CrossChainTransferStatus.FullySigned]).ToArray());
            }

            var tracker = new StatusChangeTracker();
            int newChainATip = this.NextMatureDepositHeight;

            foreach (ICrossChainTransfer partialTransfer in crossChainTransfers)
            {
                if (partialTransfer == null)
                    continue;

                if (partialTransfer.Status != CrossChainTransferStatus.Partial && partialTransfer.Status != CrossChainTransferStatus.FullySigned)
                    continue;

                List<(Transaction, TransactionData, IWithdrawal)> walletData = this.federationWalletManager.FindWithdrawalTransactions(partialTransfer.DepositTransactionId);
                if (walletData.Count == 1 && this.ValidateTransaction(walletData[0].Item1))
                {
                    Transaction walletTran = walletData[0].Item1;
                    if (walletTran.GetHash() == partialTransfer.PartialTransaction.GetHash())
                        continue;

                    if (CrossChainTransfer.TemplatesMatch(this.network, walletTran, partialTransfer.PartialTransaction))
                    {
                        partialTransfer.SetPartialTransaction(walletTran);

                        if (walletData[0].Item2.BlockHeight != null)
                            tracker.SetTransferStatus(partialTransfer, CrossChainTransferStatus.SeenInBlock, walletData[0].Item2.BlockHash, (int)walletData[0].Item2.BlockHeight);
                        else if (this.ValidateTransaction(walletTran, true))
                            tracker.SetTransferStatus(partialTransfer, CrossChainTransferStatus.FullySigned);
                        else
                            tracker.SetTransferStatus(partialTransfer, CrossChainTransferStatus.Partial);

                        continue;
                    }
                }

                // Remove any invalid withdrawal transactions.
                foreach (IWithdrawal withdrawal in walletData.Select(d => d.Item3))
                    this.federationWalletManager.RemoveTransientTransactions(withdrawal.DepositId);

                // The chain may have been rewound so that this transaction or its UTXO's have been lost.
                // Rewind our recorded chain A tip to ensure the transaction is re-built once UTXO's become available.
                if (partialTransfer.DepositHeight < newChainATip)
                    newChainATip = partialTransfer.DepositHeight ?? newChainATip;

                tracker.SetTransferStatus(partialTransfer, CrossChainTransferStatus.Suspended);
            }

            if (tracker.Count == 0)
            {
                this.logger.LogTrace("(-)[NO_CHANGES_IN_TRACKER]");
                return crossChainTransfers;
            }

            using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
            {
                dbreezeTransaction.SynchronizeTables(transferTableName, commonTableName);

                int oldChainATip = this.NextMatureDepositHeight;

                try
                {
                    foreach (KeyValuePair<ICrossChainTransfer, CrossChainTransferStatus?> kv in tracker)
                    {
                        this.PutTransfer(dbreezeTransaction, kv.Key);
                    }

                    this.SaveNextMatureHeight(dbreezeTransaction, newChainATip);
                    dbreezeTransaction.Commit();
                    this.UpdateLookups(tracker);

                    // Remove any remnants of suspended transactions from the wallet.
                    foreach (KeyValuePair<ICrossChainTransfer, CrossChainTransferStatus?> kv in tracker)
                    {
                        if (kv.Value == CrossChainTransferStatus.Suspended)
                        {
                            this.federationWalletManager.RemoveTransientTransactions(kv.Key.DepositTransactionId);
                        }
                    }

                    // Remove transient transactions after the next mature deposit height.
                    foreach ((Transaction, TransactionData, IWithdrawal) t in this.federationWalletManager.FindWithdrawalTransactions())
                    {
                        if (t.Item3.BlockNumber >= newChainATip)
                        {
                            this.federationWalletManager.RemoveTransientTransactions(t.Item3.DepositId);
                        }
                    }

                    this.federationWalletManager.SaveWallet();

                    return crossChainTransfers;
                }
                catch (Exception err)
                {
                    // Restore expected store state in case the calling code retries / continues using the store.
                    this.NextMatureDepositHeight = oldChainATip;

                    this.RollbackAndThrowTransactionError(dbreezeTransaction, err, "SANITY_ERROR");

                    // Dummy return as the above method throws. Avoids compiler error.
                    return null;
                }
            }
        }

        /// <summary>Rolls back the database if an operation running in the context of a database transaction fails.</summary>
        /// <param name="dbreezeTransaction">Database transaction to roll back.</param>
        /// <param name="exception">Exception to report and re-raise.</param>
        /// <param name="reason">Short reason/context code of failure.</param>
        private void RollbackAndThrowTransactionError(DBreeze.Transactions.Transaction dbreezeTransaction, Exception exception, string reason = "FAILED_TRANSACTION")
        {
            this.logger.LogError("Error during database update: {0}, reason: {1}", exception.Message, reason);

            dbreezeTransaction.Rollback();
            throw exception;
        }

        /// <inheritdoc />
        public Task SaveCurrentTipAsync()
        {
            return Task.Run(() =>
            {
                lock (this.lockObj)
                {
                    using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
                    {
                        dbreezeTransaction.SynchronizeTables(transferTableName, commonTableName);
                        this.SaveNextMatureHeight(dbreezeTransaction, this.NextMatureDepositHeight);
                        dbreezeTransaction.Commit();
                    }
                }
            });
        }

        /// <inheritdoc />
        public Task<bool> RecordLatestMatureDepositsAsync(IList<MaturedBlockDepositsModel> maturedBlockDeposits)
        {
            Guard.NotNull(maturedBlockDeposits, nameof(maturedBlockDeposits));
            Guard.Assert(!maturedBlockDeposits.Any(m => m.Deposits.Any(d => d.BlockNumber != m.BlockInfo.BlockHeight || d.BlockHash != m.BlockInfo.BlockHash)));

            return Task.Run(() =>
            {
                lock (this.lockObj)
                {
                    // Sanitize and sort the list.
                    int originalDepositHeight = this.NextMatureDepositHeight;

                    maturedBlockDeposits = maturedBlockDeposits
                        .OrderBy(a => a.BlockInfo.BlockHeight)
                        .SkipWhile(m => m.BlockInfo.BlockHeight < this.NextMatureDepositHeight).ToArray();

                    if (maturedBlockDeposits.Count == 0 || maturedBlockDeposits.First().BlockInfo.BlockHeight != this.NextMatureDepositHeight)
                    {
                        this.logger.LogTrace("(-)[NO_VIABLE_BLOCKS]:true");
                        return true;
                    }

                    if (maturedBlockDeposits.Last().BlockInfo.BlockHeight != this.NextMatureDepositHeight + maturedBlockDeposits.Count - 1)
                    {
                        this.logger.LogTrace("(-)[DUPLICATE_BLOCKS]:true");
                        return true;
                    }

                    this.Synchronize();

                    foreach (MaturedBlockDepositsModel maturedDeposit in maturedBlockDeposits)
                    {
                        if (maturedDeposit.BlockInfo.BlockHeight != this.NextMatureDepositHeight)
                            continue;

                        IReadOnlyList<IDeposit> deposits = maturedDeposit.Deposits;
                        if (deposits.Count == 0)
                        {
                            this.NextMatureDepositHeight++;
                            continue;
                        }

                        if (!this.federationWalletManager.IsFederationActive())
                        {
                            this.logger.LogError("The store can't persist mature deposits while the federation is inactive.");
                            continue;
                        }

                        ICrossChainTransfer[] transfers = this.ValidateCrossChainTransfers(this.Get(deposits.Select(d => d.Id).ToArray()));
                        var tracker = new StatusChangeTracker();
                        bool walletUpdated = false;

                        // Deposits are assumed to be in order of occurrence on the source chain.
                        // If we fail to build a transaction the transfer and subsequent transfers
                        // in the ordered list will be set to suspended.
                        bool haveSuspendedTransfers = false;

                        for (int i = 0; i < deposits.Count; i++)
                        {
                            // Only do work for non-existing or suspended transfers.
                            if (transfers[i] != null && transfers[i].Status != CrossChainTransferStatus.Suspended)
                            {
                                continue;
                            }

                            IDeposit deposit = deposits[i];
                            Transaction transaction = null;
                            CrossChainTransferStatus status = CrossChainTransferStatus.Suspended;
                            Script scriptPubKey = BitcoinAddress.Create(deposit.TargetAddress, this.network).ScriptPubKey;

                            if (!haveSuspendedTransfers)
                            {
                                var recipient = new Recipient
                                {
                                    Amount = deposit.Amount,
                                    ScriptPubKey = scriptPubKey
                                };

                                uint blockTime = maturedDeposit.BlockInfo.BlockTime;

                                transaction = this.withdrawalTransactionBuilder.BuildWithdrawalTransaction(deposit.Id, blockTime, recipient);

                                if (transaction != null)
                                {
                                    // Reserve the UTXOs before building the next transaction.
                                    walletUpdated |= this.federationWalletManager.ProcessTransaction(transaction, isPropagated: false);

                                    status = CrossChainTransferStatus.Partial;
                                }
                                else
                                {
                                    haveSuspendedTransfers = true;
                                }
                            }

                            if (transfers[i] == null || transaction == null)
                            {
                                transfers[i] = new CrossChainTransfer(status, deposit.Id, scriptPubKey, deposit.Amount, deposit.BlockNumber, transaction, null, null);
                                tracker.SetTransferStatus(transfers[i]);
                            }
                            else
                            {
                                transfers[i].SetPartialTransaction(transaction);
                                tracker.SetTransferStatus(transfers[i], CrossChainTransferStatus.Partial);
                            }
                        }

                        using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
                        {
                            dbreezeTransaction.SynchronizeTables(transferTableName, commonTableName);

                            int currentDepositHeight = this.NextMatureDepositHeight;

                            try
                            {
                                if (walletUpdated)
                                {
                                    this.federationWalletManager.SaveWallet();
                                }

                                // Update new or modified transfers.
                                foreach (KeyValuePair<ICrossChainTransfer, CrossChainTransferStatus?> kv in tracker)
                                {
                                    this.PutTransfer(dbreezeTransaction, kv.Key);
                                }

                                // Ensure we get called for a retry by NOT advancing the chain A tip if the block
                                // contained any suspended transfers.
                                if (!haveSuspendedTransfers)
                                {
                                    this.SaveNextMatureHeight(dbreezeTransaction, this.NextMatureDepositHeight + 1);
                                }

                                dbreezeTransaction.Commit();
                                this.UpdateLookups(tracker);
                            }
                            catch (Exception err)
                            {
                                this.logger.LogError("An error occurred when processing deposits {0}", err);

                                // Undo reserved UTXO's.
                                if (walletUpdated)
                                {
                                    foreach (KeyValuePair<ICrossChainTransfer, CrossChainTransferStatus?> kv in tracker)
                                    {
                                        if (kv.Value == CrossChainTransferStatus.Partial)
                                        {
                                            this.federationWalletManager.RemoveTransientTransactions(kv.Key.DepositTransactionId);
                                        }
                                    }

                                    this.federationWalletManager.SaveWallet();
                                }

                                // Restore expected store state in case the calling code retries / continues using the store.
                                this.NextMatureDepositHeight = currentDepositHeight;
                                this.RollbackAndThrowTransactionError(dbreezeTransaction, err, "DEPOSIT_ERROR");
                            }
                        }
                    }

                    // If progress was made we will check for more blocks.
                    return this.NextMatureDepositHeight != originalDepositHeight;
                }
            });
        }

        /// <inheritdoc />
        public Task<Transaction> MergeTransactionSignaturesAsync(uint256 depositId, Transaction[] partialTransactions)
        {
            Guard.NotNull(depositId, nameof(depositId));
            Guard.NotNull(partialTransactions, nameof(partialTransactions));

            return Task.Run(() =>
            {
                lock (this.lockObj)
                {
                    this.Synchronize();

                    this.logger.LogInformation("ValidateCrossChainTransfers : {0}", depositId);
                    ICrossChainTransfer transfer = this.ValidateCrossChainTransfers(this.Get(new[] { depositId })).FirstOrDefault();

                    if (transfer == null)
                    {
                        this.logger.LogInformation("FAILED ValidateCrossChainTransfers : {0}", depositId);

                        this.logger.LogTrace("(-)[MERGE_NOT_FOUND]:null");
                        return null;
                    }

                    if (transfer.Status != CrossChainTransferStatus.Partial)
                    {
                        this.logger.LogTrace("(-)[MERGE_BAD_STATUS]");
                        return transfer.PartialTransaction;
                    }

                    var builder = new TransactionBuilder(this.network);
                    Transaction oldTransaction = transfer.PartialTransaction;

                    transfer.CombineSignatures(builder, partialTransactions);

                    if (transfer.PartialTransaction.GetHash() == oldTransaction.GetHash())
                    {
                        this.logger.LogInformation("FAILED to combineSignatures : {0}", transfer.DepositTransactionId);

                        this.logger.LogTrace("(-)[MERGE_UNCHANGED]");
                        return transfer.PartialTransaction;
                    }

                    using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
                    {
                        try
                        {
                            dbreezeTransaction.SynchronizeTables(transferTableName, commonTableName);

                            this.federationWalletManager.ProcessTransaction(transfer.PartialTransaction);
                            this.federationWalletManager.SaveWallet();

                            if (this.ValidateTransaction(transfer.PartialTransaction, true))
                            {
                                this.logger.LogInformation("Deposit: {0} collected enough signatures and is FullySigned", transfer.DepositTransactionId);
                                transfer.SetStatus(CrossChainTransferStatus.FullySigned);
                            }

                            this.PutTransfer(dbreezeTransaction, transfer);
                            dbreezeTransaction.Commit();

                            // Do this last to maintain DB integrity. We are assuming that this won't throw.
                            this.logger.LogInformation("Deposit: {0} did not collected enough signatures and is Partial", transfer.DepositTransactionId);
                            this.TransferStatusUpdated(transfer, CrossChainTransferStatus.Partial);
                        }
                        catch (Exception err)
                        {
                            this.logger.LogError("Error: {0} ", err);

                            // Restore expected store state in case the calling code retries / continues using the store.
                            transfer.SetPartialTransaction(oldTransaction);
                            this.federationWalletManager.ProcessTransaction(oldTransaction);
                            this.federationWalletManager.SaveWallet();
                            this.RollbackAndThrowTransactionError(dbreezeTransaction, err, "MERGE_ERROR");
                        }

                        return transfer.PartialTransaction;
                    }
                }
            });
        }

        /// <summary>
        /// Uses the information contained in our chain's blocks to update the store.
        /// Sets the <see cref="CrossChainTransferStatus.SeenInBlock"/> status for transfers
        /// identified in the blocks.
        /// </summary>
        /// <param name="blocks">The blocks used to update the store. Must be sorted by ascending height leading up to the new tip.</param>
        private void Put(List<Block> blocks)
        {
            if (blocks.Count == 0)
                this.logger.LogTrace("(-)[NO_BLOCKS]:0");

            Dictionary<uint256, ICrossChainTransfer> transferLookup;
            Dictionary<uint256, IWithdrawal[]> allWithdrawals;

            int blockHeight = this.TipHashAndHeight.Height + 1;
            var allDepositIds = new HashSet<uint256>();

            allWithdrawals = new Dictionary<uint256, IWithdrawal[]>();
            foreach (Block block in blocks)
            {
                IReadOnlyList<IWithdrawal> blockWithdrawals = this.withdrawalExtractor.ExtractWithdrawalsFromBlock(block, blockHeight++);
                allDepositIds.UnionWith(blockWithdrawals.Select(d => d.DepositId).ToArray());
                allWithdrawals[block.GetHash()] = blockWithdrawals.ToArray();
            }

            // Nothing to do?
            if (allDepositIds.Count == 0)
            {
                // Exiting here and saving the tip after the sync.
                this.TipHashAndHeight = this.chain.GetBlock(blocks.Last().GetHash());

                this.logger.LogTrace("(-)[NO_DEPOSIT_IDS]");
                return;
            }

            // Create transfer lookup by deposit Id.
            uint256[] uniqueDepositIds = allDepositIds.ToArray();
            ICrossChainTransfer[] uniqueTransfers = this.Get(uniqueDepositIds);
            transferLookup = new Dictionary<uint256, ICrossChainTransfer>();
            for (int i = 0; i < uniqueDepositIds.Length; i++)
                transferLookup[uniqueDepositIds[i]] = uniqueTransfers[i];


            // Only create a transaction if there is important work to do.
            using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
            {
                dbreezeTransaction.SynchronizeTables(transferTableName, commonTableName);

                ChainedHeader prevTip = this.TipHashAndHeight;

                try
                {
                    var tracker = new StatusChangeTracker();

                    // Find transfer transactions in blocks
                    foreach (Block block in blocks)
                    {
                        // First check the database to see if we already know about these deposits.
                        IWithdrawal[] withdrawals = allWithdrawals[block.GetHash()].ToArray();
                        ICrossChainTransfer[] crossChainTransfers = withdrawals.Select(d => transferLookup[d.DepositId]).ToArray();

                        // Update the information about these deposits or record their status.
                        for (int i = 0; i < crossChainTransfers.Length; i++)
                        {
                            IWithdrawal withdrawal = withdrawals[i];
                            Transaction transaction = block.Transactions.Single(t => t.GetHash() == withdrawal.Id);

                            // Ensure that the wallet is in step.
                            this.federationWalletManager.ProcessTransaction(transaction, withdrawal.BlockNumber, block);

                            if (crossChainTransfers[i] == null)
                            {
                                Script scriptPubKey = BitcoinAddress.Create(withdrawal.TargetAddress, this.network).ScriptPubKey;

                                crossChainTransfers[i] = new CrossChainTransfer(CrossChainTransferStatus.SeenInBlock, withdrawal.DepositId,
                                    scriptPubKey, withdrawal.Amount, null, transaction, withdrawal.BlockHash, withdrawal.BlockNumber);

                                tracker.SetTransferStatus(crossChainTransfers[i]);
                            }
                            else
                            {
                                crossChainTransfers[i].SetPartialTransaction(transaction);

                                tracker.SetTransferStatus(crossChainTransfers[i],
                                    CrossChainTransferStatus.SeenInBlock, withdrawal.BlockHash, withdrawal.BlockNumber);
                            }
                        }
                    }

                    // Write transfers.
                    this.PutTransfers(dbreezeTransaction, tracker.Keys.ToArray());

                    // Commit additions
                    ChainedHeader newTip = this.chain.GetBlock(blocks.Last().GetHash());
                    this.SaveTipHashAndHeight(dbreezeTransaction, newTip);
                    dbreezeTransaction.Commit();

                    // Update the lookups last to ensure store integrity.
                    this.UpdateLookups(tracker);
                }
                catch (Exception err)
                {
                    // Restore expected store state in case the calling code retries / continues using the store.
                    this.TipHashAndHeight = prevTip;
                    this.RollbackAndThrowTransactionError(dbreezeTransaction, err, "PUT_ERROR");
                }
            }
        }

        /// <summary>
        /// Used to handle reorg (if required) and revert status from <see cref="CrossChainTransferStatus.SeenInBlock"/> to
        /// <see cref="CrossChainTransferStatus.FullySigned"/>. Also returns a flag to indicate whether we are behind the current tip.
        /// </summary>
        /// <returns>Returns <c>true</c> if a rewind was performed and <c>false</c> otherwise.</returns>
        private bool RewindIfRequired()
        {
            HashHeightPair tipToChase = this.TipToChase();

            if (tipToChase.Hash == this.TipHashAndHeight.HashBlock)
            {
                // Indicate that we are synchronized.
                this.logger.LogTrace("(-)[SYNCHRONIZED]:false");
                return false;
            }

            // We are dependent on the wallet manager having dealt with any fork by now.
            if (this.chain.GetBlock(tipToChase.Hash) == null)
            {
                ICollection<uint256> locators = this.federationWalletManager.GetWallet().BlockLocator;
                var blockLocator = new BlockLocator { Blocks = locators.ToList() };
                ChainedHeader fork = this.chain.FindFork(blockLocator);
                this.federationWalletManager.RemoveBlocks(fork);
                tipToChase = this.TipToChase();
            }

            // If the chain does not contain our tip.
            if (this.TipHashAndHeight != null && (this.TipHashAndHeight.Height > tipToChase.Height ||
                this.chain.GetBlock(this.TipHashAndHeight.HashBlock)?.Height != this.TipHashAndHeight.Height))
            {
                // We are ahead of the current chain or on the wrong chain.
                ChainedHeader fork = this.chain.FindFork(this.TipHashAndHeight.GetLocator()) ?? this.chain.GetBlock(0);

                // Must not exceed wallet height otherise transaction validations may fail.
                while (fork.Height > tipToChase.Height)
                    fork = fork.Previous;

                using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
                {
                    dbreezeTransaction.SynchronizeTables(transferTableName, commonTableName);
                    dbreezeTransaction.ValuesLazyLoadingIsOn = false;

                    ChainedHeader prevTip = this.TipHashAndHeight;

                    try
                    {
                        StatusChangeTracker tracker = this.OnDeleteBlocks(dbreezeTransaction, fork.Height);
                        this.SaveTipHashAndHeight(dbreezeTransaction, fork);
                        dbreezeTransaction.Commit();
                        this.UndoLookups(tracker);
                    }
                    catch (Exception err)
                    {
                        // Restore expected store state in case the calling code retries / continues using the store.
                        this.TipHashAndHeight = prevTip;
                        this.RollbackAndThrowTransactionError(dbreezeTransaction, err, "REWIND_ERROR");
                    }
                }

                this.ValidateCrossChainTransfers();
                return true;
            }

            // Indicate that we are behind the current chain.
            return false;
        }

        /// <summary>Attempts to synchronizes the store with the chain.</summary>
        /// <returns>Returns <c>true</c> if the store is in sync or <c>false</c> otherwise.</returns>
        private bool Synchronize()
        {
            lock (this.lockObj)
            {
                HashHeightPair tipToChase = this.TipToChase();
                if (tipToChase.Hash == this.TipHashAndHeight.HashBlock)
                {
                    // Indicate that we are synchronized.
                    this.logger.LogTrace("(-)[SYNCHRONIZED]:true");
                    return true;
                }

                while (!this.cancellation.IsCancellationRequested)
                {
                    if (this.HasSuspended())
                    {
                        ICrossChainTransfer[] transfers = this.Get(this.depositsIdsByStatus[CrossChainTransferStatus.Suspended].ToArray());
                        this.NextMatureDepositHeight = transfers.Min(t => t.DepositHeight) ?? this.NextMatureDepositHeight;
                    }

                    this.RewindIfRequired();

                    if (this.SynchronizeBatch())
                    {
                        using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
                        {
                            dbreezeTransaction.SynchronizeTables(transferTableName, commonTableName);

                            this.SaveTipHashAndHeight(dbreezeTransaction, this.TipHashAndHeight);

                            dbreezeTransaction.Commit();
                        }

                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Synchronize with a batch of blocks.</summary>
        /// <returns>Returns <c>true</c> if we match the chain tip and <c>false</c> if we are behind the tip.</returns>
        private bool SynchronizeBatch()
        {
            // Get a batch of blocks.
            var blockHashes = new List<uint256>();
            int batchSize = 0;
            HashHeightPair tipToChase = this.TipToChase();

            foreach (ChainedHeader header in this.chain.EnumerateToTip(this.TipHashAndHeight.HashBlock).Skip(1))
            {
                if (this.chain.GetBlock(header.HashBlock) == null)
                    break;

                if (header.Height > tipToChase.Height)
                    break;

                blockHashes.Add(header.HashBlock);

                if (++batchSize >= synchronizationBatchSize)
                    break;
            }

            List<Block> blocks = this.blockRepository.GetBlocksAsync(blockHashes).GetAwaiter().GetResult();
            int availableBlocks = blocks.FindIndex(b => (b == null));
            if (availableBlocks < 0)
                availableBlocks = blocks.Count;

            if (availableBlocks > 0)
            {
                Block lastBlock = blocks[availableBlocks - 1];
                this.Put(blocks.GetRange(0, availableBlocks));
                this.logger.LogInformation("Synchronized {0} blocks with cross-chain store to advance tip to block {1}", availableBlocks, this.TipHashAndHeight?.Height);
            }

            bool done = availableBlocks < synchronizationBatchSize;

            return done;
        }

        /// <summary>Loads the tip and hash height.</summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <returns>The hash and height pair.</returns>
        private ChainedHeader LoadTipHashAndHeight(DBreeze.Transactions.Transaction dbreezeTransaction)
        {
            var blockLocator = new BlockLocator();
            try
            {
                Row<byte[], byte[]> row = dbreezeTransaction.Select<byte[], byte[]>(commonTableName, RepositoryTipKey);
                Guard.Assert(row.Exists);
                blockLocator.FromBytes(row.Value);
            }
            catch (Exception)
            {
                blockLocator.Blocks = new List<uint256> { this.network.GenesisHash };
            }

            this.TipHashAndHeight = this.chain.GetBlock(blockLocator.Blocks[0]) ?? this.chain.FindFork(blockLocator);
            return this.TipHashAndHeight;
        }

        /// <summary>Saves the tip and hash height.</summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="newTip">The new tip to persist.</param>
        private void SaveTipHashAndHeight(DBreeze.Transactions.Transaction dbreezeTransaction, ChainedHeader newTip)
        {
            BlockLocator locator = this.chain.Tip.GetLocator();
            this.TipHashAndHeight = newTip;
            dbreezeTransaction.Insert<byte[], byte[]>(commonTableName, RepositoryTipKey, locator.ToBytes());
        }

        /// <summary>Loads the counter-chain next mature block height.</summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <returns>The hash and height pair.</returns>
        private int LoadNextMatureHeight(DBreeze.Transactions.Transaction dbreezeTransaction)
        {
            Row<byte[], int> row = dbreezeTransaction.Select<byte[], int>(commonTableName, NextMatureTipKey);
            if (row.Exists)
                this.NextMatureDepositHeight = row.Value;

            return this.NextMatureDepositHeight;
        }

        /// <summary>Saves the counter-chain next mature block height.</summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="newTip">The next mature block height on the counter-chain.</param>
        private void SaveNextMatureHeight(DBreeze.Transactions.Transaction dbreezeTransaction, int newTip)
        {
            this.NextMatureDepositHeight = newTip;
            dbreezeTransaction.Insert<byte[], int>(commonTableName, NextMatureTipKey, this.NextMatureDepositHeight);
        }

        /// <inheritdoc />
        public Task<ICrossChainTransfer[]> GetAsync(uint256[] depositIds)
        {
            return Task.Run(() =>
            {
                this.Synchronize();

                ICrossChainTransfer[] res = this.ValidateCrossChainTransfers(this.Get(depositIds));
                return res;
            });
        }

        private ICrossChainTransfer[] Get(uint256[] depositId)
        {
            using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
            {
                dbreezeTransaction.ValuesLazyLoadingIsOn = false;

                return this.Get(dbreezeTransaction, depositId);
            }
        }

        private CrossChainTransfer[] Get(DBreeze.Transactions.Transaction transaction, uint256[] depositId)
        {
            Guard.NotNull(depositId, nameof(depositId));

            // To boost performance we will access the deposits sorted by deposit id.
            var depositDict = new Dictionary<uint256, int>();
            for (int i = 0; i < depositId.Length; i++)
                depositDict[depositId[i]] = i;

            var byteListComparer = new ByteListComparer();
            List<KeyValuePair<uint256, int>> depositList = depositDict.ToList();
            depositList.Sort((pair1, pair2) => byteListComparer.Compare(pair1.Key.ToBytes(), pair2.Key.ToBytes()));

            var res = new CrossChainTransfer[depositId.Length];

            foreach (KeyValuePair<uint256, int> kv in depositList)
            {
                Row<byte[], byte[]> transferRow = transaction.Select<byte[], byte[]>(transferTableName, kv.Key.ToBytes());

                if (transferRow.Exists)
                {
                    var crossChainTransfer = new CrossChainTransfer();
                    crossChainTransfer.FromBytes(transferRow.Value, this.network.Consensus.ConsensusFactory);
                    res[kv.Value] = crossChainTransfer;
                }
            }

            return res;
        }

        private OutPoint EarliestOutput(Transaction transaction)
        {
            Comparer<OutPoint> comparer = Comparer<OutPoint>.Create((x, y) => this.federationWalletManager.CompareOutpoints(x, y));
            return transaction.Inputs.Select(i => i.PrevOut).OrderByDescending(t => t, comparer).FirstOrDefault();
        }

        private ICrossChainTransfer[] GetTransfersByStatus(CrossChainTransferStatus[] statuses, bool sort = false, bool validate = true)
        {
            lock (this.lockObj)
            {
                this.Synchronize();

                var depositIds = new HashSet<uint256>();
                foreach (CrossChainTransferStatus status in statuses)
                    depositIds.UnionWith(this.depositsIdsByStatus[status]);

                uint256[] partialTransferHashes = depositIds.ToArray();
                ICrossChainTransfer[] partialTransfers = this.Get(partialTransferHashes).Where(t => t != null).ToArray();

                if (validate)
                {
                    this.ValidateCrossChainTransfers(partialTransfers);
                    partialTransfers = partialTransfers.Where(t => statuses.Contains(t.Status)).ToArray();
                }

                if (!sort)
                {
                    return partialTransfers;
                }

                return partialTransfers.OrderBy(t => this.EarliestOutput(t.PartialTransaction), Comparer<OutPoint>.Create((x, y) =>
                    this.federationWalletManager.CompareOutpoints(x, y))).ToArray();
            }
        }

        /// <inheritdoc />
        public Task<Dictionary<uint256, Transaction>> GetTransactionsByStatusAsync(CrossChainTransferStatus status, bool sort = false)
        {
            return Task.Run(() =>
            {
                ICrossChainTransfer[] res = this.GetTransfersByStatus(new[] { status }, sort);
                return res.Where(t => t.PartialTransaction != null).ToDictionary(t => t.DepositTransactionId, t => t.PartialTransaction);
            });
        }

        /// <summary>Persist the cross-chain transfer information into the database.</summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="crossChainTransfer">Cross-chain transfer information to be inserted.</param>
        private void PutTransfer(DBreeze.Transactions.Transaction dbreezeTransaction, ICrossChainTransfer crossChainTransfer)
        {
            Guard.NotNull(crossChainTransfer, nameof(crossChainTransfer));

            byte[] crossChainTransferBytes = this.dBreezeSerializer.Serialize(crossChainTransfer);

            dbreezeTransaction.Insert<byte[], byte[]>(transferTableName, crossChainTransfer.DepositTransactionId.ToBytes(), crossChainTransferBytes);
        }

        /// <summary>Persist multiple cross-chain transfer information into the database.</summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="crossChainTransfers">Cross-chain transfers to be inserted.</param>
        private void PutTransfers(DBreeze.Transactions.Transaction dbreezeTransaction, ICrossChainTransfer[] crossChainTransfers)
        {
            Guard.NotNull(crossChainTransfers, nameof(crossChainTransfers));

            // Optimal ordering for DB consumption.
            var byteListComparer = new ByteListComparer();
            List<ICrossChainTransfer> orderedTransfers = crossChainTransfers.ToList();
            orderedTransfers.Sort((pair1, pair2) => byteListComparer.Compare(pair1.DepositTransactionId.ToBytes(), pair2.DepositTransactionId.ToBytes()));

            // Write each transfer in order.
            foreach (ICrossChainTransfer transfer in orderedTransfers)
            {
                byte[] transferBytes = this.dBreezeSerializer.Serialize(transfer);
                dbreezeTransaction.Insert<byte[], byte[]>(transferTableName, transfer.DepositTransactionId.ToBytes(), transferBytes);
            }
        }

        /// <summary>Deletes the cross-chain transfer information from the database</summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="crossChainTransfer">Cross-chain transfer information to be deleted.</param>
        private void DeleteTransfer(DBreeze.Transactions.Transaction dbreezeTransaction, ICrossChainTransfer crossChainTransfer)
        {
            Guard.NotNull(crossChainTransfer, nameof(crossChainTransfer));

            dbreezeTransaction.RemoveKey<byte[]>(transferTableName, crossChainTransfer.DepositTransactionId.ToBytes());
        }

        /// <summary>
        /// Forgets transfer information for the blocks being removed and returns information for updating the transient lookups.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="lastBlockHeight">The last block to retain.</param>
        /// <returns>A tracker with all the cross chain transfers that were affected.</returns>
        private StatusChangeTracker OnDeleteBlocks(DBreeze.Transactions.Transaction dbreezeTransaction, int lastBlockHeight)
        {
            // Gather all the deposit ids that may have had transactions in the blocks being deleted.
            var depositIds = new HashSet<uint256>();
            uint256[] blocksToRemove = this.blockHeightsByBlockHash.Where(a => a.Value > lastBlockHeight).Select(a => a.Key).ToArray();

            foreach (HashSet<uint256> deposits in blocksToRemove.Select(a => this.depositIdsByBlockHash[a]))
            {
                depositIds.UnionWith(deposits);
            }

            // Find the transfers related to these deposit ids in the database.
            var tracker = new StatusChangeTracker();
            CrossChainTransfer[] crossChainTransfers = this.Get(dbreezeTransaction, depositIds.ToArray());

            foreach (CrossChainTransfer transfer in crossChainTransfers)
            {
                // Transfers that only exist in the DB due to having been seen in a block should be removed completely.
                if (transfer.DepositHeight == null)
                {
                    // Trigger deletion from the status lookup.
                    tracker.SetTransferStatus(transfer);

                    // Delete the transfer completely.
                    this.DeleteTransfer(dbreezeTransaction, transfer);
                }
                else
                {
                    // Transaction is no longer seen.
                    tracker.SetTransferStatus(transfer, CrossChainTransferStatus.FullySigned);

                    // Write the transfer status to the database.
                    this.PutTransfer(dbreezeTransaction, transfer);
                }
            }

            return tracker;
        }

        /// <summary>Updates the status lookup based on a transfer and its previous status.</summary>
        /// <param name="transfer">The cross-chain transfer that was update.</param>
        /// <param name="oldStatus">The old status.</param>
        private void TransferStatusUpdated(ICrossChainTransfer transfer, CrossChainTransferStatus? oldStatus)
        {
            if (oldStatus != null)
            {
                this.depositsIdsByStatus[(CrossChainTransferStatus)oldStatus].Remove(transfer.DepositTransactionId);
            }

            this.depositsIdsByStatus[transfer.Status].Add(transfer.DepositTransactionId);
        }

        /// <summary>Update the transient lookups after changes have been committed to the store.</summary>
        /// <param name="tracker">Information about how to update the lookups.</param>
        private void UpdateLookups(StatusChangeTracker tracker)
        {
            foreach (uint256 hash in tracker.UniqueBlockHashes())
            {
                this.depositIdsByBlockHash[hash] = new HashSet<uint256>();
            }

            foreach (KeyValuePair<ICrossChainTransfer, CrossChainTransferStatus?> kv in tracker)
            {
                this.TransferStatusUpdated(kv.Key, kv.Value);

                if (kv.Key.BlockHash != null && kv.Key.BlockHeight != null)
                {
                    if (!this.depositIdsByBlockHash[kv.Key.BlockHash].Contains(kv.Key.DepositTransactionId))
                        this.depositIdsByBlockHash[kv.Key.BlockHash].Add(kv.Key.DepositTransactionId);
                    this.blockHeightsByBlockHash[kv.Key.BlockHash] = (int)kv.Key.BlockHeight;
                }
            }
        }

        /// <summary>Undoes the transient lookups after block removals have been committed to the store.</summary>
        /// <param name="tracker">Information about how to undo the lookups.</param>
        private void UndoLookups(StatusChangeTracker tracker)
        {
            foreach (KeyValuePair<ICrossChainTransfer, CrossChainTransferStatus?> kv in tracker)
            {
                if (kv.Value == null)
                {
                    this.depositsIdsByStatus[kv.Key.Status].Remove(kv.Key.DepositTransactionId);
                }

                this.TransferStatusUpdated(kv.Key, kv.Value);
            }

            foreach (uint256 hash in tracker.UniqueBlockHashes())
            {
                this.depositIdsByBlockHash.Remove(hash);
                this.blockHeightsByBlockHash.Remove(hash);
            }
        }

        public bool ValidateTransaction(Transaction transaction, bool checkSignature = false)
        {
            return this.federationWalletManager.ValidateTransaction(transaction, checkSignature);
        }

        /// <inheritdoc />
        public Dictionary<CrossChainTransferStatus, int> GetCrossChainTransferStatusCounter()
        {
            Dictionary<CrossChainTransferStatus, int> result = new Dictionary<CrossChainTransferStatus, int>();
            foreach (CrossChainTransferStatus status in Enum.GetValues(typeof(CrossChainTransferStatus)).Cast<CrossChainTransferStatus>())
            {
                result[status] = this.depositsIdsByStatus.TryGet(status)?.Count ?? 0;
            }

            return result;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.SaveCurrentTipAsync().GetAwaiter().GetResult();
            this.cancellation.Cancel();
            this.DBreeze.Dispose();
        }
    }
}
