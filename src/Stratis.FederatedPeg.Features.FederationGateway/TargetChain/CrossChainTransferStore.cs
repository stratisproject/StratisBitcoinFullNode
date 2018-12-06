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
using NBitcoin.Policy;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Wallet;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
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
        private Dictionary<uint256, HashSet<uint256>> depositIdsByBlockHash = new Dictionary<uint256, HashSet<uint256>>();

        /// <summary>This contains the block heights by block hashes for only the blocks of interest in our chain.</summary>
        private Dictionary<uint256, int> blockHeightsByBlockHash = new Dictionary<uint256, int>();

        /// <summary>This table contains deposits ids by status.</summary>
        private Dictionary<CrossChainTransferStatus, HashSet<uint256>> depositsIdsByStatus = new Dictionary<CrossChainTransferStatus, HashSet<uint256>>();

        /// <inheritdoc />
        public int NextMatureDepositHeight { get; private set; }

        /// <inheritdoc />
        public ChainedHeader TipHashAndHeight { get; private set; }

        public BlockLocator BlockLocator { get; private set; }

        /// <summary>The key of the repository tip in the common table.</summary>
        private static readonly byte[] RepositoryTipKey = new byte[] { 0 };

        /// <summary>The key of the counter-chain last mature block tip in the common table.</summary>
        private static readonly byte[] NextMatureTipKey = new byte[] { 1 };

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine DBreeze;
        private readonly Network network;
        private readonly ConcurrentChain chain;
        private readonly IWithdrawalExtractor withdrawalExtractor;
        private readonly IBlockRepository blockRepository;
        private readonly CancellationTokenSource cancellation;
        private readonly IFederationWalletManager federationWalletManager;
        private readonly IFederationWalletTransactionHandler federationWalletTransactionHandler;
        private readonly IFederationGatewaySettings federationGatewaySettings;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly object lockObj;

        public CrossChainTransferStore(Network network, DataFolder dataFolder, ConcurrentChain chain, IFederationGatewaySettings settings, IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory, IWithdrawalExtractor withdrawalExtractor, IFullNode fullNode, IBlockRepository blockRepository,
            IFederationWalletManager federationWalletManager, IFederationWalletTransactionHandler federationWalletTransactionHandler)
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
            Guard.NotNull(federationWalletTransactionHandler, nameof(federationWalletTransactionHandler));

            this.network = network;
            this.chain = chain;
            this.dateTimeProvider = dateTimeProvider;
            this.blockRepository = blockRepository;
            this.federationWalletManager = federationWalletManager;
            this.federationWalletTransactionHandler = federationWalletTransactionHandler;
            this.federationGatewaySettings = settings;
            this.withdrawalExtractor = withdrawalExtractor;
            this.lockObj = new object();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.TipHashAndHeight = this.chain.GetBlock(0);
            this.NextMatureDepositHeight = 1;
            this.cancellation = new CancellationTokenSource();

            // Future-proof store name.
            var depositStoreName = "federatedTransfers" + settings.MultiSigAddress.ToString();
            string folder = Path.Combine(dataFolder.RootPath, depositStoreName);
            Directory.CreateDirectory(folder);
            this.DBreeze = new DBreezeEngine(folder);

            // Initialize tracking deposits by status.
            foreach (var status in typeof(CrossChainTransferStatus).GetEnumValues())
                this.depositsIdsByStatus[(CrossChainTransferStatus)status] = new HashSet<uint256>();
        }

        /// <summary>
        /// Performs any needed initialisation for the database.
        /// </summary>
        public void Initialize()
        {
            lock (this.lockObj)
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
                {
                    dbreezeTransaction.ValuesLazyLoadingIsOn = false;

                    this.LoadTipHashAndHeight(dbreezeTransaction);
                    this.LoadNextMatureHeight(dbreezeTransaction);

                    // Initialize the lookups.
                    foreach (Row<byte[], byte[]> transferRow in dbreezeTransaction.SelectForward<byte[], byte[]>(transferTableName))
                    {
                        CrossChainTransfer transfer = new CrossChainTransfer();
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

                this.logger.LogTrace("(-)");
            }
        }

        /// <summary>
        /// Starts the cross-chain-transfer store.
        /// </summary>
        public void Start()
        {
            lock (this.lockObj)
            {
                Guard.Assert(this.Synchronize());
            }
        }

        /// <inheritdoc />
        public bool HasSuspended()
        {
            return this.depositsIdsByStatus[CrossChainTransferStatus.Suspended].Count != 0;
        }

        /// <inheritdoc />
        public bool CanPersistMatureDeposits()
        {
            return this.federationWalletManager.IsFederationActive();
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
                return new HashHeightPair(this.network.GenesisHash, 0);
            }

            return new HashHeightPair(wallet.LastBlockSyncedHash, (int)wallet.LastBlockSyncedHeight);
        }

        /// <summary>
        /// Partial or fully signed transfers should have their source UTXO's recorded by an up-to-date wallet.
        /// Sets transfers to <see cref="CrossChainTransferStatus.Rejected"/> if their UTXO's are not reserved
        /// within the wallet.
        /// </summary>
        /// <param name="crossChainTransfers">The transfers to check. If not supplied then all partial and fully signed transfers are checked.</param>
        /// <returns>Returns the list of transfers, possible with updated statuses.</returns>
        private ICrossChainTransfer[] ValidateCrossChainTransfers(ICrossChainTransfer[] crossChainTransfers = null)
        {
            FederationWallet wallet = this.federationWalletManager.GetWallet();

            if (crossChainTransfers == null)
            {
                crossChainTransfers = Get(
                    this.depositsIdsByStatus[CrossChainTransferStatus.Partial].Union(
                        this.depositsIdsByStatus[CrossChainTransferStatus.FullySigned]).ToArray());
            }

            var tracker = new StatusChangeTracker();
            int newChainATip = this.NextMatureDepositHeight;

            foreach (CrossChainTransfer partialTransfer in crossChainTransfers)
            {
                if (partialTransfer == null)
                    continue;

                if (partialTransfer.Status != CrossChainTransferStatus.Partial && partialTransfer.Status != CrossChainTransferStatus.FullySigned)
                    continue;

                // Verify that the transaction input UTXO's have been reserved by the wallet.
                if (ValidateTransaction(partialTransfer.PartialTransaction))
                    continue;

                // If not then the chain has been rewound so far that this transaction has lost its UTXO's.
                // Rewind our recorded chain A tip to ensure the transaction is re-built once UTXO's become available.
                if (partialTransfer.DepositHeight < newChainATip)
                    newChainATip = partialTransfer.DepositHeight ?? newChainATip;

                tracker.SetTransferStatus(partialTransfer, CrossChainTransferStatus.Suspended);
            }

            if (tracker.Count == 0)
                return crossChainTransfers;

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

                    // Remove any remnants of the transaction from the wallet.
                    foreach (KeyValuePair<ICrossChainTransfer, CrossChainTransferStatus?> kv in tracker)
                    {
                        if (kv.Value == CrossChainTransferStatus.Suspended)
                        {
                            this.federationWalletManager.RemoveTransaction(kv.Key.PartialTransaction);
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

        private Transaction BuildDeterministicTransaction(uint256 depositId, Recipient recipient)
        {
            try
            {
                this.logger.LogTrace("()");

                // Build the multisig transaction template.
                uint256 opReturnData = depositId;
                string walletPassword = this.federationWalletManager.Secret.WalletPassword;
                var multiSigContext = new TransactionBuildContext(new[] { recipient }.ToList(), opReturnData: opReturnData.ToBytes())
                {
                    OrderCoinsDeterministic = true,
                    TransactionFee = this.federationGatewaySettings.TransactionFee,
                    MinConfirmations = this.federationGatewaySettings.MinCoinMaturity,
                    Shuffle = false,
                    IgnoreVerify = true,
                    WalletPassword = walletPassword,
                    Sign = (walletPassword ?? "") != ""
                };

                // Build the transaction.
                Transaction transaction = this.federationWalletTransactionHandler.BuildTransaction(multiSigContext);

                this.logger.LogTrace("(-)");
                return transaction;
            }
            catch (Exception error)
            {
                this.logger.LogTrace("Could not create transaction for deposit {0}: {1}", depositId, error.Message);
            }

            return null;
        }

        /// <summary>
        /// Rolls back the database if an operation running in the context of a database transaction fails.
        /// </summary>
        /// <param name="dbreezeTransaction">Database transaction to roll back.</param>
        /// <param name="exception">Exception to report and re-raise.</param>
        /// <param name="reason">Short reason/context code of failure.</param>
        private void RollbackAndThrowTransactionError(DBreeze.Transactions.Transaction dbreezeTransaction, Exception exception, string reason = "FAILED_TRANSACTION")
        {
            this.logger.LogError("Error during database update: {0}", exception.Message);
            this.logger.LogTrace("(-):[{0}]", reason);

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
        public Task<bool> RecordLatestMatureDepositsAsync(IMaturedBlockDeposits[] maturedBlockDeposits)
        {
            Guard.NotNull(maturedBlockDeposits, nameof(maturedBlockDeposits));
            Guard.Assert(!maturedBlockDeposits.Any(m => m.Deposits.Any(d => d.BlockNumber != m.Block.BlockHeight || d.BlockHash != m.Block.BlockHash)));

            return Task.Run(() =>
            {
                lock (this.lockObj)
                {
                    this.logger.LogTrace("()");

                    // Sanitize and sort the list.
                    int originalDepositHeight = this.NextMatureDepositHeight;

                    maturedBlockDeposits = maturedBlockDeposits
                        .OrderBy(a => a.Block.BlockHeight)
                        .SkipWhile(m => m.Block.BlockHeight < this.NextMatureDepositHeight).ToArray();

                    if (maturedBlockDeposits.Length == 0 ||
                        maturedBlockDeposits.First().Block.BlockHeight != this.NextMatureDepositHeight)
                    {
                        this.logger.LogTrace("(-):[NO_VIABLE_BLOCKS]");
                        return true;
                    }

                    if (maturedBlockDeposits.Last().Block.BlockHeight != this.NextMatureDepositHeight + maturedBlockDeposits.Length - 1)
                    {
                        this.logger.LogTrace("(-):[DUPLICATE_BLOCKS]");
                        return true;
                    }

                    this.Synchronize();

                    FederationWallet wallet = this.federationWalletManager.GetWallet();
                    bool? canPersist = null;

                    for (int j = 0; j < maturedBlockDeposits.Length; j++)
                    {
                        if (maturedBlockDeposits[j].Block.BlockHeight != this.NextMatureDepositHeight)
                            continue;

                        IReadOnlyList<IDeposit> deposits = maturedBlockDeposits[j].Deposits;
                        if (deposits.Count == 0)
                        {
                            this.NextMatureDepositHeight++;
                            continue;
                        }

                        // CanPersistMatureDeposits is a bit slow. Call it only once.
                        canPersist = canPersist ?? this.CanPersistMatureDeposits();

                        if (!(bool)canPersist)
                        {
                            this.logger.LogError("The store can't persist mature deposits at the moment.");
                            this.logger.LogTrace("(-)");
                            continue;
                        }

                        ICrossChainTransfer[] transfers = this.ValidateCrossChainTransfers(this.Get(deposits.Select(d => d.Id).ToArray()));
                        var tracker = new StatusChangeTracker();
                        bool walletUpdated = false;

                        // Deposits are assumed to be in order of occurrence on the source chain.
                        // If we fail to build a transacion the transfer and subsequent transfers
                        // in the orderd list will be set to suspended.
                        bool haveSuspendedTransfers = false;

                        for (int i = 0; i < deposits.Count; i++)
                        {
                            // Check if the deposits already exist which could happen if it was found on the chain.
                            if (transfers[i] != null && transfers[i].Status != CrossChainTransferStatus.Suspended)
                                continue;

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

                                // Verify whether the transaction is somehow already in the wallet.
                                TransactionData transactionData;
                                (transaction, transactionData) = this.FindWithdrawalTransactionInWallet(deposit.Id);
                                if (transaction != null)
                                {
                                    if (transactionData.BlockHash != null)
                                    {
                                        status = CrossChainTransferStatus.SeenInBlock;
                                    }
                                    else
                                    {
                                        // Add my signature.
                                        var scriptCoins = new List<Coin>();
                                        if (this.TransactionHasValidUTXOs(transaction, scriptCoins))
                                        {
                                            TransactionBuilder builder = new TransactionBuilder(this.network);

                                            transaction = builder.AddCoins(scriptCoins).SignTransaction(transaction);

                                            status = this.ValidateTransaction(transaction, true) ? CrossChainTransferStatus.FullySigned : CrossChainTransferStatus.Partial;
                                        }
                                    }
                                }
                                else
                                {
                                    transaction = BuildDeterministicTransaction(deposit.Id, recipient);

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
                            }

                            if (transfers[i] == null)
                            {
                                transfers[i] = new CrossChainTransfer(status, deposit.Id, scriptPubKey, deposit.Amount, deposit.BlockNumber, transaction, null, null);
                                tracker.SetTransferStatus(transfers[i]);
                            }
                            else if (transaction != null)
                            {
                                transfers[i].SetPartialTransaction(transaction);
                                tracker.SetTransferStatus(transfers[i], CrossChainTransferStatus.Partial);
                            }
                            else
                            {
                                // If we can't fix suspended transfers then exit this loop now.
                                break;
                            }
                        }

                        using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
                        {
                            dbreezeTransaction.SynchronizeTables(transferTableName, commonTableName);

                            int currentDepositHeight = this.NextMatureDepositHeight;

                            try
                            {
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
                                // Undo reserved UTXO's.
                                if (walletUpdated)
                                {
                                    foreach (KeyValuePair<ICrossChainTransfer, CrossChainTransferStatus?> kv in tracker)
                                    {
                                        if (kv.Value == CrossChainTransferStatus.Partial)
                                        {
                                            this.federationWalletManager.RemoveTransaction(kv.Key.PartialTransaction);
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

                    this.logger.LogTrace("(-)");

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
                    this.logger.LogTrace("()");
                    this.Synchronize();

                    FederationWallet wallet = this.federationWalletManager.GetWallet();
                    ICrossChainTransfer transfer = this.ValidateCrossChainTransfers(this.Get(new[] { depositId })).FirstOrDefault();

                    if (transfer == null)
                    {
                        this.logger.LogTrace("(-)[MERGE_NOTFOUND]");
                        return null;
                    }

                    if (transfer.Status != CrossChainTransferStatus.Partial)
                    {
                        this.logger.LogTrace("(-)[MERGE_BADSTATUS]");
                        return transfer.PartialTransaction;
                    }

                    var builder = new TransactionBuilder(this.network);
                    Transaction oldTransaction = transfer.PartialTransaction;

                    transfer.CombineSignatures(builder, partialTransactions);

                    if (transfer.PartialTransaction.GetHash() == oldTransaction.GetHash())
                    {
                        this.logger.LogTrace("(-)[MERGE_UNCHANGED]");
                        return transfer.PartialTransaction;
                    }

                    using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
                    {
                        try
                        {
                            dbreezeTransaction.SynchronizeTables(transferTableName, commonTableName);

                            this.federationWalletManager.UpdateTransientTransactionDetails(oldTransaction.GetHash(), transfer.PartialTransaction);
                            this.federationWalletManager.SaveWallet();

                            if (ValidateTransaction(transfer.PartialTransaction, true))
                            {
                                transfer.SetStatus(CrossChainTransferStatus.FullySigned);
                            }

                            this.PutTransfer(dbreezeTransaction, transfer);
                            dbreezeTransaction.Commit();

                            // Do this last to maintain DB integrity. We are assuming that this won't throw.
                            this.TransferStatusUpdated(transfer, CrossChainTransferStatus.Partial);
                        }
                        catch (Exception err)
                        {
                            // Restore expected store state in case the calling code retries / continues using the store.
                            transfer.SetPartialTransaction(oldTransaction);
                            this.federationWalletManager.UpdateTransientTransactionDetails(transfer.PartialTransaction.GetHash(), oldTransaction);
                            this.federationWalletManager.SaveWallet();
                            this.RollbackAndThrowTransactionError(dbreezeTransaction, err, "MERGE_ERROR");
                        }

                        this.logger.LogTrace("(-)");
                        return transfer?.PartialTransaction;
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
            this.logger.LogTrace("()");

            if (blocks.Count == 0)
                this.logger.LogTrace("(-):0");

            Dictionary<uint256, ICrossChainTransfer> transferLookup;
            Dictionary<uint256, IWithdrawal[]> allWithdrawals;
            {
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

                    this.logger.LogTrace("(-)");
                    return;
                }

                // Create transfer lookup by deposit Id.
                uint256[] uniqueDepositIds = allDepositIds.ToArray();
                ICrossChainTransfer[] uniqueTransfers = this.Get(uniqueDepositIds);
                transferLookup = new Dictionary<uint256, ICrossChainTransfer>();
                for (int i = 0; i < uniqueDepositIds.Length; i++)
                    transferLookup[uniqueDepositIds[i]] = uniqueTransfers[i];
            }

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

                            if (crossChainTransfers[i] == null)
                            {
                                Script scriptPubKey = BitcoinAddress.Create(withdrawal.TargetAddress, this.network).ScriptPubKey;
                                Transaction transaction = block.Transactions.Single(t => t.GetHash() == withdrawal.Id);
                                crossChainTransfers[i] = new CrossChainTransfer(CrossChainTransferStatus.SeenInBlock, withdrawal.DepositId,
                                    scriptPubKey, withdrawal.Amount, null, transaction, withdrawal.BlockHash, withdrawal.BlockNumber);

                                tracker.SetTransferStatus(crossChainTransfers[i]);
                            }
                            else
                            {
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

            this.logger.LogTrace("(-):{0}", blocks.Count);
        }

        /// <summary>
        /// Used to handle reorg (if required) and revert status from <see cref="CrossChainTransferStatus.SeenInBlock"/> to
        /// <see cref="CrossChainTransferStatus.FullySigned"/>. Also returns a flag to indicate whether we are behind the current tip.
        /// </summary>
        /// <returns>
        /// Returns <c>true</c> if a rewind was performed and <c>false</c> otherwise.
        /// </returns>
        private bool RewindIfRequired()
        {
            this.logger.LogTrace("()");

            HashHeightPair tipToChase = this.TipToChase();

            if (tipToChase.Hash == this.TipHashAndHeight.HashBlock)
            {
                // Indicate that we are synchronized.
                this.logger.LogTrace("(-):false");
                return false;
            }

            // We are dependent on the wallet manager having dealt with any fork by now.
            if (this.chain.GetBlock(tipToChase.Hash) == null)
            {
                ICollection<uint256> locators = this.federationWalletManager.GetWallet().BlockLocator;
                BlockLocator blockLocator = new BlockLocator { Blocks = locators.ToList() };
                ChainedHeader fork = this.chain.FindFork(blockLocator);
                this.federationWalletManager.RemoveBlocks(fork);
                tipToChase = this.TipToChase();
            }

            // If the chain does not contain our tip..
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
                this.logger.LogTrace("(-):true");
                return true;
            }

            // Indicate that we are behind the current chain.
            this.logger.LogTrace("(-):false");
            return false;
        }

        /// <summary>
        /// Attempts to synchronizes the store with the chain.
        /// </summary>
        /// <returns>
        /// Returns <c>true</c> if the store is in sync or <c>false</c> otherwise.
        /// </returns>
        private bool Synchronize()
        {
            lock (this.lockObj)
            {
                this.logger.LogTrace("()");

                HashHeightPair tipToChase = this.TipToChase();
                if (tipToChase.Hash == this.TipHashAndHeight.HashBlock)
                {
                    // Indicate that we are synchronized.
                    this.logger.LogTrace("(-):true");
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

                        this.logger.LogTrace("(-):true");
                        return true;
                    }
                }

                this.logger.LogTrace("(-):false");
                return false;
            }
        }

        /// <summary>
        /// Synchronize with a batch of blocks.
        /// </summary>
        /// <returns>Returns <c>true</c> if we match the chain tip and <c>false</c> if we are behind the tip.</returns>
        private bool SynchronizeBatch()
        {
            this.logger.LogTrace("()");

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
                this.logger.LogInformation("Synchronized {0} blocks with cross-chain store to advance tip to block {1}", availableBlocks, this.TipHashAndHeight.Height);
            }

            bool done = availableBlocks < synchronizationBatchSize;

            this.logger.LogTrace("(-):{0}", done);
            return done;
        }

        /// <summary>
        /// Loads the tip and hash height.
        /// </summary>
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

        /// <summary>
        /// Saves the tip and hash height.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="newTip">The new tip to persist.</param>
        private void SaveTipHashAndHeight(DBreeze.Transactions.Transaction dbreezeTransaction, ChainedHeader newTip)
        {
            BlockLocator locator = this.chain.Tip.GetLocator();
            this.TipHashAndHeight = newTip;
            dbreezeTransaction.Insert<byte[], byte[]>(commonTableName, RepositoryTipKey, locator.ToBytes());
        }

        /// <summary>
        /// Loads the counter-chain next mature block height.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <returns>The hash and height pair.</returns>
        private int LoadNextMatureHeight(DBreeze.Transactions.Transaction dbreezeTransaction)
        {
            Row<byte[], int> row = dbreezeTransaction.Select<byte[], int>(commonTableName, NextMatureTipKey);
            if (row.Exists)
                this.NextMatureDepositHeight = row.Value;

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
        public Task<ICrossChainTransfer[]> GetAsync(uint256[] depositIds)
        {
            return Task.Run(() =>
            {
                this.logger.LogTrace("()");

                this.Synchronize();

                ICrossChainTransfer[] res = this.ValidateCrossChainTransfers(this.Get(depositIds));

                this.logger.LogTrace("(-)");
                return res;
            });
        }

        private ICrossChainTransfer[] Get(uint256[] depositId)
        {
            using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
            {
                dbreezeTransaction.ValuesLazyLoadingIsOn = false;

                return Get(dbreezeTransaction, depositId);
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
                    CrossChainTransfer crossChainTransfer = new CrossChainTransfer();
                    crossChainTransfer.FromBytes(transferRow.Value, this.network.Consensus.ConsensusFactory);
                    res[kv.Value] = crossChainTransfer;
                }
            }

            return res;
        }

        /// <summary>
        /// Identifies the earliest multisig transaction data input associated with a transaction.
        /// </summary>
        /// <param name="wallet">The wallet to look in.</param>
        /// <param name="transaction">The transaction to find the earliest multisig transaction data input for.</param>
        /// <returns>The earliest multisig transaction data input.</returns>
        private TransactionData MultiSigInput(FederationWallet wallet, Transaction transaction)
        {
            foreach (TxIn input in transaction.Inputs)
            {
                Wallet.TransactionData transactionData = wallet.MultiSigAddress.Transactions
                    .Where(t => t?.SpendingDetails?.TransactionId == transaction.GetHash() && t.Id == input.PrevOut.Hash && t.Index == input.PrevOut.N)
                    .FirstOrDefault();

                if (transactionData != null)
                    return transactionData;
            }

            return null;
        }

        /// <inheritdoc />
        public Task<Dictionary<uint256, Transaction>> GetTransactionsByStatusAsync(CrossChainTransferStatus status)
        {
            return Task.Run(() =>
            {
                lock (this.lockObj)
                {
                    this.logger.LogTrace("()");

                    this.Synchronize();

                    uint256[] partialTransferHashes = this.depositsIdsByStatus[status].ToArray();
                    ICrossChainTransfer[] partialTransfers = this.Get(partialTransferHashes).ToArray();

                    if (status == CrossChainTransferStatus.Partial || status == CrossChainTransferStatus.FullySigned)
                    {
                        this.ValidateCrossChainTransfers(partialTransfers);
                        partialTransfers = partialTransfers.Where(t => t.Status == status).ToArray();
                    }

                    this.logger.LogTrace("(-)");
                    return partialTransfers
                        .Where(t => t.PartialTransaction != null)
                        .ToDictionary(t => t.DepositTransactionId, t => t.PartialTransaction);
                }
            });
        }

        /// <summary>
        /// Persist the cross-chain transfer information into the database.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="crossChainTransfer">Cross-chain transfer information to be inserted.</param>
        private void PutTransfer(DBreeze.Transactions.Transaction dbreezeTransaction, ICrossChainTransfer crossChainTransfer)
        {
            Guard.NotNull(crossChainTransfer, nameof(crossChainTransfer));

            this.logger.LogTrace("()");

            dbreezeTransaction.Insert<byte[], ICrossChainTransfer>(transferTableName, crossChainTransfer.DepositTransactionId.ToBytes(), crossChainTransfer);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Persist multiple cross-chain transfer information into the database.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="crossChainTransfers">Cross-chain transfers to be inserted.</param>
        private void PutTransfers(DBreeze.Transactions.Transaction dbreezeTransaction, ICrossChainTransfer[] crossChainTransfers)
        {
            Guard.NotNull(crossChainTransfers, nameof(crossChainTransfers));

            this.logger.LogTrace("()");

            // Optimal ordering for DB consumption.
            var byteListComparer = new ByteListComparer();
            List<ICrossChainTransfer> orderedTransfers = crossChainTransfers.ToList();
            orderedTransfers.Sort((pair1, pair2) => byteListComparer.Compare(pair1.DepositTransactionId.ToBytes(), pair2.DepositTransactionId.ToBytes()));

            // Write each transfer in order.
            foreach (ICrossChainTransfer transfer in orderedTransfers)
            {
                dbreezeTransaction.Insert(transferTableName, transfer.DepositTransactionId.ToBytes(), transfer);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Deletes the cross-chain transfer information from the database
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="crossChainTransfer">Cross-chain transfer information to be deleted.</param>
        private void DeleteTransfer(DBreeze.Transactions.Transaction dbreezeTransaction, ICrossChainTransfer crossChainTransfer)
        {
            Guard.NotNull(crossChainTransfer, nameof(crossChainTransfer));

            this.logger.LogTrace("()");

            dbreezeTransaction.RemoveKey<byte[]>(transferTableName, crossChainTransfer.DepositTransactionId.ToBytes());

            this.logger.LogTrace("(-)");
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

        /// <summary>
        /// Updates the status lookup based on a transfer and its previous status.
        /// </summary>
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

        /// <summary>
        /// Update the transient lookups after changes have been committed to the store.
        /// </summary>
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

        /// <summary>
        /// Undoes the transient lookups after block removals have been committed to the store.
        /// </summary>
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

        /// <summary>
        /// Determines if there is an existing valid withdrawal transaction for a given deposit id.
        /// </summary>
        /// <param name="depositId">The deposit id to find the withrawal transaction for.</param>
        /// <returns>The transaction data containing the withdrawal transaction.</returns>
        private (Transaction, TransactionData) FindWithdrawalTransactionInWallet(uint256 depositId)
        {
            FederationWallet wallet = this.federationWalletManager.GetWallet();

            foreach (TransactionData transactionData in wallet.MultiSigAddress.Transactions)
            {
                Transaction walletTran = transactionData.GetFullTransaction(this.network);
                IWithdrawal withdrawal = this.withdrawalExtractor.ExtractWithdrawalFromTransaction(walletTran, 0, 0);
                if (withdrawal?.DepositId == depositId)
                {
                    if (!TransactionHasValidUTXOs(walletTran))
                        continue;

                    return (walletTran, transactionData);
                }
            }

            return (null, null);
        }

        private bool TransactionHasValidUTXOs(Transaction transaction, List<Coin> coins = null)
        {
            // All the input UTXO's should be present in spending details of the multi-sig address.
            FederationWallet wallet = this.federationWalletManager.GetWallet();

            foreach (TxIn input in transaction.Inputs)
            {
                foreach (TransactionData transactionData in wallet.MultiSigAddress.Transactions
                    .Where(t => t.SpendingDetails != null && t.Id == input.PrevOut.Hash && t.Index == input.PrevOut.N))
                {
                    // Check that the previous outputs are only spent by this transaction.
                    if (transactionData == null || transactionData.SpendingDetails.TransactionId != transaction.GetHash())
                        return false;

                    coins?.Add(new Coin(transactionData.Id, (uint)transactionData.Index, transactionData.Amount, transactionData.ScriptPubKey));
                }
            }

            return true;
        }

        /// <inheritdoc />
        public bool ValidateTransaction(Transaction transaction, bool checkSignature = false)
        {
            // All the input UTXO's should be present in spending details of the multi-sig address.
            List<Coin> coins = checkSignature ? new List<Coin>() : null;
            FederationWallet wallet = this.federationWalletManager.GetWallet();

            // Verify that the transaction has valid UTXOs.
            if (!TransactionHasValidUTXOs(transaction, coins))
                return false;

            // Verify that the deposit does not have an earlier transaction.
            IWithdrawal myWithdrawal = this.withdrawalExtractor.ExtractWithdrawalFromTransaction(transaction, 0, 0);
            (Transaction walletTran, _) = this.FindWithdrawalTransactionInWallet(myWithdrawal.DepositId);
            if (walletTran != null && walletTran.GetHash() != transaction.GetHash())
                return false;

            // Verify that all inputs are signed.
            if (checkSignature)
            {
                TransactionBuilder builder = new TransactionBuilder(wallet.Network).AddCoins(coins);
                if (!builder.Verify(transaction, new Money(0.01m, MoneyUnit.BTC), out TransactionPolicyError[] errors))
                {
                    return false;
                }
            }

            return true;
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
