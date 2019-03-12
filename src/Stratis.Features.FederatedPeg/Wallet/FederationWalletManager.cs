using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Policy;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.FederationWallet.Tests")]

namespace Stratis.Features.FederatedPeg.Wallet
{
    /// <summary>
    /// A class that represents a flat view of the wallets history.
    /// </summary>
    public class FlatHistory
    {
        /// <summary>
        /// The address associated with this UTXO
        /// </summary>
        public MultiSigAddress Address { get; set; }

        /// <summary>
        /// The transaction representing the UTXO.
        /// </summary>
        public TransactionData Transaction { get; set; }
    }

    /// <summary>
    /// Credentials to the federation wallet.
    /// </summary>
    public class WalletSecret
    {
        /// <summary>The federation wallet's password, needed for getting the private key which is used for signing federation transactions.</summary>
        public string WalletPassword { get; set; }
    }

    /// <summary>
    /// A manager providing operations on wallets.
    /// </summary>
    public class FederationWalletManager : IFederationWalletManager
    {
        /// <summary>Timer for saving wallet files to the file system.</summary>
        private const int WalletSavetimeIntervalInMinutes = 5;

        /// <summary>
        /// A lock object that protects access to the <see cref="FederationWallet"/>.
        /// Any of the collections inside Wallet must be synchronized using this lock.
        /// </summary>
        private readonly object lockObject;

        /// <summary>The async loop we need to wait upon before we can shut down this manager.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>Gets the wallet.</summary>
        public FederationWallet Wallet { get; set; }

        /// <summary>The type of coin used in this manager.</summary>
        private readonly CoinType coinType;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>The chain of headers.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>The withdrawal extractor used to extract withdrawals from transactions.</summary>
        private readonly IWithdrawalExtractor withdrawalExtractor;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>An object capable of storing <see cref="FederationWallet"/>s to the file system.</summary>
        private readonly FileStorage<FederationWallet> fileStorage;

        /// <summary>The broadcast manager.</summary>
        private readonly IBroadcasterManager broadcasterManager;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Indicates whether the federation is active.</summary>
        private bool isFederationActive;

        public uint256 WalletTipHash { get; set; }

        public bool ContainsWallets => throw new NotImplementedException();

        /// <summary>
        /// Credentials for the wallet. Initially unpopulated on node startup, has to be provided by the user.
        /// </summary>
        public WalletSecret Secret { get; set; }

        /// <summary>
        /// The name of the watch-only wallet as saved in the file system.
        /// </summary>
        private const string WalletFileName = "multisig_wallet.json";

        // In order to allow faster look-ups of transactions affecting the wallets' addresses,
        // we keep a couple of objects in memory:
        // 1. the list of unspent outputs for checking whether inputs from a transaction are being spent by our wallet and
        // 2. the list of addresses contained in our wallet for checking whether a transaction is being paid to the wallet.
        private readonly Dictionary<OutPoint, TransactionData> outpointLookup;
        //    internal Dictionary<Script, MultiSigAddress> multiSigKeysLookup;

        // Gateway settings picked up from the node config.
        private readonly IFederationGatewaySettings federationGatewaySettings;

        public FederationWalletManager(
            ILoggerFactory loggerFactory,
            Network network,
            ConcurrentChain chain,
            DataFolder dataFolder,
            IWalletFeePolicy walletFeePolicy,
            IAsyncLoopFactory asyncLoopFactory,
            INodeLifetime nodeLifetime,
            IDateTimeProvider dateTimeProvider,
            IFederationGatewaySettings federationGatewaySettings,
            IWithdrawalExtractor withdrawalExtractor,
            IBroadcasterManager broadcasterManager = null) // no need to know about transactions the node broadcasted
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(dataFolder, nameof(dataFolder));
            Guard.NotNull(walletFeePolicy, nameof(walletFeePolicy));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(federationGatewaySettings, nameof(federationGatewaySettings));
            Guard.NotNull(withdrawalExtractor, nameof(withdrawalExtractor));

            this.lockObject = new object();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chain = chain;
            this.asyncLoopFactory = asyncLoopFactory;
            this.nodeLifetime = nodeLifetime;
            this.fileStorage = new FileStorage<FederationWallet>(dataFolder.WalletPath);
            this.broadcasterManager = broadcasterManager;
            this.dateTimeProvider = dateTimeProvider;
            this.federationGatewaySettings = federationGatewaySettings;
            this.withdrawalExtractor = withdrawalExtractor;
            this.outpointLookup = new Dictionary<OutPoint, TransactionData>();
            this.isFederationActive = false;

            // register events
            if (this.broadcasterManager != null)
            {
                this.broadcasterManager.TransactionStateChanged += this.BroadcasterManager_TransactionStateChanged;
            }
        }

        private void BroadcasterManager_TransactionStateChanged(object sender, TransactionBroadcastEntry transactionEntry)
        {
            this.ProcessTransaction(transactionEntry.Transaction, null, null, transactionEntry.State == State.Propagated);
        }

        public void Start()
        {
            // Find the wallet and load it in memory.
            if (this.fileStorage.Exists(WalletFileName))
                this.Wallet = this.fileStorage.LoadByFileName(WalletFileName);
            else
            {
                // Create the multisig wallet file if it doesn't exist
                this.Wallet = this.GenerateWallet();
                this.SaveWallet();
            }

            // Load data in memory for faster lookups.
            this.LoadKeysLookupLock();

            // find the last chain block received by the wallet manager.
            this.WalletTipHash = this.LastReceivedBlockHash();

            // save the wallets file every 5 minutes to help against crashes.
            this.asyncLoop = this.asyncLoopFactory.Run("wallet persist job", token =>
            {
                this.SaveWallet();
                this.logger.LogInformation("Wallets saved to file at {0}.", this.dateTimeProvider.GetUtcNow());

                return Task.CompletedTask;
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpan.FromMinutes(WalletSavetimeIntervalInMinutes),
            startAfter: TimeSpan.FromMinutes(WalletSavetimeIntervalInMinutes));
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (this.broadcasterManager != null)
                this.broadcasterManager.TransactionStateChanged -= this.BroadcasterManager_TransactionStateChanged;

            this.asyncLoop?.Dispose();
            this.SaveWallet();
        }

        /// <inheritdoc />
        public IEnumerable<FlatHistory> GetHistory()
        {
            FlatHistory[] items = null;
            lock (this.lockObject)
            {
                // Get transactions contained in the wallet.
                items = this.Wallet.MultiSigAddress.Transactions.Select(t => new FlatHistory { Address = this.Wallet.MultiSigAddress, Transaction = t }).ToArray();
            }

            return items;
        }

        /// <inheritdoc />
        public int LastBlockHeight()
        {
            if (this.Wallet == null)
            {
                int height = this.chain.Tip.Height;
                this.logger.LogTrace("(-)[NO_WALLET]:{0}", height);
                return height;
            }

            int res = this.Wallet.LastBlockSyncedHeight ?? 0;
            return res;
        }

        /// <summary>
        /// Gets the hash of the last block received by the wallets.
        /// </summary>
        /// <returns>Hash of the last block received by the wallets.</returns>
        public uint256 LastReceivedBlockHash()
        {
            if (this.Wallet == null)
            {
                uint256 hash = this.chain.Tip.HashBlock;
                this.logger.LogTrace("(-)[NO_WALLET]:'{0}'", hash);
                return hash;
            }

            uint256 lastBlockSyncedHash = this.Wallet.LastBlockSyncedHash;

            if (lastBlockSyncedHash == null)
            {
                lastBlockSyncedHash = this.chain.Tip.HashBlock;
            }

            return lastBlockSyncedHash;
        }

        /// <inheritdoc />
        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWallet(int confirmations = 0)
        {
            if (this.Wallet == null)
            {
                return Enumerable.Empty<Wallet.UnspentOutputReference>();
            }

            UnspentOutputReference[] res;
            lock (this.lockObject)
            {
                res = this.Wallet.GetSpendableTransactions(this.chain.Tip.Height, confirmations).ToArray();
            }

            return res;
        }

        /// <inheritdoc />
        public void RemoveBlocks(ChainedHeader fork)
        {
            Guard.NotNull(fork, nameof(fork));

            lock (this.lockObject)
            {
                // Remove all the UTXO that have been reorged.
                IEnumerable<TransactionData> makeUnspendable = this.Wallet.MultiSigAddress.Transactions.Where(w => w.BlockHeight > fork.Height).ToList();
                foreach (TransactionData transactionData in makeUnspendable)
                    this.Wallet.MultiSigAddress.Transactions.Remove(transactionData);

                // Bring back all the UTXO that are now spendable after the reorg.
                IEnumerable<TransactionData> makeSpendable = this.Wallet.MultiSigAddress.Transactions.Where(w => (w.SpendingDetails != null) && (w.SpendingDetails.BlockHeight > fork.Height));
                foreach (TransactionData transactionData in makeSpendable)
                    transactionData.SpendingDetails = null;

                this.UpdateLastBlockSyncedHeight(fork);
            }
        }


        /// <inheritdoc />
        public void ProcessBlock(Block block, ChainedHeader chainedHeader)
        {
            Guard.NotNull(block, nameof(block));
            Guard.NotNull(chainedHeader, nameof(chainedHeader));

            // If there is no wallet yet, update the wallet tip hash and do nothing else.
            if (this.Wallet == null)
            {
                this.WalletTipHash = chainedHeader.HashBlock;
                this.logger.LogTrace("(-)[NO_WALLET]");
                return;
            }

            // Is this the next block.
            if (chainedHeader.Header.HashPrevBlock != this.WalletTipHash)
            {
                this.logger.LogTrace("New block's previous hash '{0}' does not match current wallet's tip hash '{1}'.", chainedHeader.Header.HashPrevBlock, this.WalletTipHash);

                // Are we still on the main chain.
                ChainedHeader current = this.chain.GetBlock(this.WalletTipHash);
                if (current == null)
                {
                    this.logger.LogTrace("(-)[REORG]");
                    throw new WalletException("Reorg");
                }

                // The block coming in to the wallet should never be ahead of the wallet.
                // If the block is behind, let it pass.
                if (chainedHeader.Height > current.Height)
                {
                    this.logger.LogTrace("(-)[BLOCK_TOO_FAR]");
                    throw new WalletException("block too far in the future has arrived to the wallet");
                }
            }

            lock (this.lockObject)
            {
                bool walletUpdated = false;
                foreach (Transaction transaction in block.Transactions.Where(t => !(t.IsCoinBase && t.TotalOut == Money.Zero)))
                {
                    bool trxFound = this.ProcessTransaction(transaction, chainedHeader.Height, block, true);
                    if (trxFound)
                    {
                        walletUpdated = true;
                    }
                }

                // Update the wallets with the last processed block height.
                // It's important that updating the height happens after the block processing is complete,
                // as if the node is stopped, on re-opening it will start updating from the previous height.
                this.UpdateLastBlockSyncedHeight(chainedHeader);

                if (walletUpdated)
                {
                    this.SaveWallet();
                }
            }
        }

        /// <inheritdoc />
        public bool ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null, bool isPropagated = true)
        {
            Guard.NotNull(transaction, nameof(transaction));
            uint256 hash = transaction.GetHash();

            if (this.Wallet == null)
            {
                this.logger.LogTrace("(-)");
                return false;
            }

            bool foundReceivingTrx = false, foundSendingTrx = false;

            lock (this.lockObject)
            {
                // Extract the withdrawal from the transaction (if any).
                IWithdrawal withdrawal = this.withdrawalExtractor.ExtractWithdrawalFromTransaction(transaction, block?.GetHash(), blockHeight ?? 0);
                if (withdrawal != null)
                {
                    // Exit if already present and included in a block.
                    List<(Transaction, TransactionData, IWithdrawal)> walletData = this.FindWithdrawalTransactions(withdrawal.DepositId);
                    if ((walletData.Count == 1) && (walletData[0].Item2.BlockHeight != null))
                        return false;

                    // Remove this to prevent duplicates if the transaction hash has changed.
                    if (walletData.Count != 0)
                        this.RemoveTransientTransactions(withdrawal.DepositId);
                }

                // Check the outputs.
                foreach (TxOut utxo in transaction.Outputs)
                {
                    // Check if the outputs contain one of our addresses.
                    if (this.Wallet.MultiSigAddress.ScriptPubKey == utxo.ScriptPubKey)
                    {
                        this.AddTransactionToWallet(transaction, utxo, blockHeight, block, isPropagated);
                        foundReceivingTrx = true;
                    }
                }

                // Check the inputs - include those that have a reference to a transaction containing one of our scripts and the same index.
                foreach (TxIn input in transaction.Inputs)
                {
                    if (!this.outpointLookup.TryGetValue(input.PrevOut, out TransactionData tTx))
                    {
                        continue;
                    }

                    // Get the details of the outputs paid out.
                    IEnumerable<TxOut> paidOutTo = transaction.Outputs.Where(o =>
                    {
                        // If script is empty ignore it.
                        if (o.IsEmpty)
                            return false;

                        // Check if the destination script is one of the wallet's.
                        // TODO fix this
                        bool found = this.Wallet.MultiSigAddress.ScriptPubKey == o.ScriptPubKey;

                        // Include the keys not included in our wallets (external payees).
                        if (!found)
                            return true;

                        // Include the keys that are in the wallet but that are for receiving
                        // addresses (which would mean the user paid itself).
                        // We also exclude the keys involved in a staking transaction.
                        //return !addr.IsChangeAddress() && !transaction.IsCoinStake;
                        return true;
                    });

                    this.AddSpendingTransactionToWallet(transaction, paidOutTo, tTx.Id, tTx.Index, blockHeight, block);
                    foundSendingTrx = true;
                }
            }

            // Figure out what to do when this transaction is found to affect the wallet.
            if (foundSendingTrx || foundReceivingTrx)
            {
                // Save the wallet when the transaction was not included in a block.
                if (blockHeight == null)
                {
                    this.SaveWallet();
                }
            }

            return foundSendingTrx || foundReceivingTrx;
        }

        /// <inheritdoc />
        public bool RemoveTransaction(Transaction transaction)
        {
            Guard.NotNull(transaction, nameof(transaction));
            uint256 hash = transaction.GetHash();

            bool updatedWallet = false;

            // Check the inputs - include those that have a reference to a transaction containing one of our scripts and the same index.
            foreach (TxIn input in transaction.Inputs)
            {
                if (!this.outpointLookup.TryGetValue(input.PrevOut, out TransactionData tTx))
                {
                    continue;
                }

                // Get the transaction being spent and unspend it.
                TransactionData spentTransaction = this.Wallet.MultiSigAddress.Transactions.SingleOrDefault(t => (t.Id == tTx.Id) && (t.Index == tTx.Index));
                if (spentTransaction != null)
                {
                    spentTransaction.SpendingDetails = null;
                    spentTransaction.MerkleProof = null;
                    updatedWallet = true;
                }
            }

            foreach (TxOut utxo in transaction.Outputs)
            {
                // Check if the outputs contain one of our addresses.
                if (this.Wallet.MultiSigAddress.ScriptPubKey == utxo.ScriptPubKey)
                {
                    int index = transaction.Outputs.IndexOf(utxo);

                    // Remove any UTXO's that were provided by this transaction from wallet.
                    TransactionData foundTransaction = this.Wallet.MultiSigAddress.Transactions.FirstOrDefault(t => (t.Id == hash) && (t.Index == index));
                    if (foundTransaction != null)
                    {
                        this.RemoveInputKeysLookupLock(foundTransaction);
                        this.Wallet.MultiSigAddress.Transactions.Remove(foundTransaction);
                        updatedWallet = true;
                    }
                }
            }

            return updatedWallet;
        }

        /// <inheritdoc />
        public HashSet<(uint256, DateTimeOffset)> RemoveAllTransactions()
        {
            var removedTransactions = new HashSet<(uint256, DateTimeOffset)>();

            lock (this.lockObject)
            {
                removedTransactions = this.Wallet.MultiSigAddress.Transactions.Select(t => (t.Id, t.CreationTime)).ToHashSet();
                this.Wallet.MultiSigAddress.Transactions.Clear();
            }

            if (removedTransactions.Any())
            {
                this.SaveWallet();
            }

            return removedTransactions;
        }

        /// <summary>
        /// Adds a transaction that credits the wallet with new coins.
        /// This method is can be called many times for the same transaction (idempotent).
        /// </summary>
        /// <param name="transaction">The transaction from which details are added.</param>
        /// <param name="utxo">The unspent output to add to the wallet.</param>
        /// <param name="blockHeight">Height of the block.</param>
        /// <param name="block">The block containing the transaction to add.</param>
        /// <param name="isPropagated">Propagation state of the transaction.</param>
        private void AddTransactionToWallet(Transaction transaction, TxOut utxo, int? blockHeight = null, Block block = null, bool isPropagated = true)
        {
            Guard.NotNull(transaction, nameof(transaction));
            Guard.NotNull(utxo, nameof(utxo));

            uint256 transactionHash = transaction.GetHash();

            // Get the collection of transactions to add to.
            Script script = utxo.ScriptPubKey;

            // Check if a similar UTXO exists or not (same transaction ID and same index).
            // New UTXOs are added, existing ones are updated.
            int index = transaction.Outputs.IndexOf(utxo);
            Money amount = utxo.Value;
            TransactionData foundTransaction = this.Wallet.MultiSigAddress.Transactions.FirstOrDefault(t => (t.Id == transactionHash) && (t.Index == index));
            if (foundTransaction == null)
            {
                this.logger.LogTrace("UTXO '{0}-{1}' not found, creating.", transactionHash, index);
                TransactionData newTransaction = new TransactionData
                {
                    Amount = amount,
                    BlockHeight = blockHeight,
                    BlockHash = block?.GetHash(),
                    Id = transactionHash,
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? transaction.Time),
                    Index = index,
                    ScriptPubKey = script,
                    Hex = transaction.ToHex(),
                    IsPropagated = isPropagated
                };

                // Add the Merkle proof to the (non-spending) transaction.
                if (block != null)
                {
                    newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                }

                this.Wallet.MultiSigAddress.Transactions.Add(newTransaction);
                this.AddInputKeysLookupLock(newTransaction);
            }
            else
            {
                this.logger.LogTrace("Transaction ID '{0}' found, updating.", transactionHash);

                // Update the block height and block hash.
                if ((foundTransaction.BlockHeight == null) && (blockHeight != null))
                {
                    foundTransaction.BlockHeight = blockHeight;
                    foundTransaction.BlockHash = block?.GetHash();
                }

                // Update the block time.
                if (block != null)
                {
                    foundTransaction.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
                }

                // Add the Merkle proof now that the transaction is confirmed in a block.
                if ((block != null) && (foundTransaction.MerkleProof == null))
                {
                    foundTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                }

                if (isPropagated)
                    foundTransaction.IsPropagated = true;
            }

            this.TransactionFoundInternal(script);
        }

        /// <summary>
        /// Mark an output as spent, the credit of the output will not be used to calculate the balance.
        /// The output will remain in the wallet for history (and reorg).
        /// </summary>
        /// <param name="transaction">The transaction from which details are added.</param>
        /// <param name="paidToOutputs">A list of payments made out</param>
        /// <param name="spendingTransactionId">The id of the transaction containing the output being spent, if this is a spending transaction.</param>
        /// <param name="spendingTransactionIndex">The index of the output in the transaction being referenced, if this is a spending transaction.</param>
        /// <param name="blockHeight">Height of the block.</param>
        /// <param name="block">The block containing the transaction to add.</param>
        private void AddSpendingTransactionToWallet(Transaction transaction, IEnumerable<TxOut> paidToOutputs,
            uint256 spendingTransactionId, int? spendingTransactionIndex, int? blockHeight = null, Block block = null)
        {
            Guard.NotNull(transaction, nameof(transaction));
            Guard.NotNull(paidToOutputs, nameof(paidToOutputs));

            // Get the transaction being spent.
            TransactionData spentTransaction = this.Wallet.MultiSigAddress.Transactions.SingleOrDefault(t => (t.Id == spendingTransactionId) && (t.Index == spendingTransactionIndex));
            if (spentTransaction == null)
            {
                // Strange, why would it be null?
                this.logger.LogTrace("(-)[TX_NULL]");
                return;
            }

            // If the details of this spending transaction are seen for the first time.
            if (spentTransaction.SpendingDetails == null)
            {
                this.logger.LogTrace("Spending UTXO '{0}-{1}' is new.", spendingTransactionId, spendingTransactionIndex);

                List<PaymentDetails> payments = new List<PaymentDetails>();
                foreach (TxOut paidToOutput in paidToOutputs)
                {
                    // Figure out how to retrieve the destination address.
                    string destinationAddress = string.Empty;
                    ScriptTemplate scriptTemplate = paidToOutput.ScriptPubKey.FindTemplate(this.network);
                    switch (scriptTemplate.Type)
                    {
                        // Pay to PubKey can be found in outputs of staking transactions.
                        case TxOutType.TX_PUBKEY:
                            PubKey pubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(paidToOutput.ScriptPubKey);
                            destinationAddress = pubKey.GetAddress(this.network).ToString();
                            break;
                        // Pay to PubKey hash is the regular, most common type of output.
                        case TxOutType.TX_PUBKEYHASH:
                            destinationAddress = paidToOutput.ScriptPubKey.GetDestinationAddress(this.network).ToString();
                            break;
                        case TxOutType.TX_NONSTANDARD:
                        case TxOutType.TX_SCRIPTHASH:
                            destinationAddress = paidToOutput.ScriptPubKey.GetDestinationAddress(this.network).ToString();
                            break;
                        case TxOutType.TX_MULTISIG:
                        case TxOutType.TX_NULL_DATA:
                        case TxOutType.TX_SEGWIT:
                            break;
                    }

                    payments.Add(new PaymentDetails
                    {
                        DestinationScriptPubKey = paidToOutput.ScriptPubKey,
                        DestinationAddress = destinationAddress,
                        Amount = paidToOutput.Value
                    });
                }

                SpendingDetails spendingDetails = new SpendingDetails
                {
                    TransactionId = transaction.GetHash(),
                    Payments = payments,
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? transaction.Time),
                    BlockHeight = blockHeight,
                    Hex = transaction.ToHex(),
                    IsCoinStake = transaction.IsCoinStake == false ? (bool?)null : true
                };

                spentTransaction.SpendingDetails = spendingDetails;
                spentTransaction.MerkleProof = null;
            }
            else // If this spending transaction is being confirmed in a block.
            {
                this.logger.LogTrace("Spending transaction ID '{0}' is being confirmed, updating.", spendingTransactionId);

                // Update the block height.
                if (spentTransaction.SpendingDetails.BlockHeight == null && blockHeight != null)
                {
                    spentTransaction.SpendingDetails.BlockHeight = blockHeight;
                }

                // Update the block time to be that of the block in which the transaction is confirmed.
                if (block != null)
                {
                    spentTransaction.SpendingDetails.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
                }
            }
        }

        /// <summary>
        /// Loads the keys and transactions we're tracking in memory for faster lookups.
        /// </summary>
        public void LoadKeysLookupLock()
        {
            lock (this.lockObject)
            {
                foreach (TransactionData transaction in this.Wallet.MultiSigAddress.Transactions)
                {
                    this.outpointLookup[new OutPoint(transaction.Id, transaction.Index)] = transaction;
                }
            }
        }

        /// <summary>
        /// Add to the list of unspent outputs kept in memory for faster lookups.
        /// </summary>
        private void AddInputKeysLookupLock(TransactionData transactionData)
        {
            Guard.NotNull(transactionData, nameof(transactionData));

            lock (this.lockObject)
            {
                this.outpointLookup[new OutPoint(transactionData.Id, transactionData.Index)] = transactionData;
            }
        }

        /// <summary>
        /// Remove from the list of unspent outputs kept in memory for faster lookups.
        /// </summary>
        private void RemoveInputKeysLookupLock(TransactionData transactionData)
        {
            Guard.NotNull(transactionData, nameof(transactionData));

            lock (this.lockObject)
            {
                this.outpointLookup.Remove(new OutPoint(transactionData.Id, transactionData.Index));
            }
        }

        public void TransactionFoundInternal(Script script)
        {
            // Persists the wallet file.
            this.SaveWallet();
        }

        /// <inheritdoc />
        public void SaveWallet()
        {
            if (this.Wallet != null)
            {
                lock (this.lockObject)
                {
                    this.fileStorage.SaveToFile(this.Wallet, WalletFileName);
                }
            }
        }

        /// <inheritdoc />
        public bool RemoveTransientTransactions(uint256 depositId = null)
        {
            lock (this.lockObject)
            {
                // Remove transient transactions not seen in a block yet.
                bool walletUpdated = false;

                foreach ((Transaction transaction, TransactionData transactionData, _) in this.FindWithdrawalTransactions(depositId)
                    .Where(w => w.Item2.BlockHash == null))
                {
                    Guard.Assert(transactionData.SpendingDetails == null);
                    walletUpdated |= this.RemoveTransaction(transaction);
                }

                return walletUpdated;
            }
        }

        /// <inheritdoc />
        public IEnumerable<IWithdrawal> GetWithdrawals()
        {
            foreach (TransactionData transactionData in this.Wallet.MultiSigAddress.Transactions.OrderByDescending(t => t.CreationTime))
            {
                Transaction walletTrx = transactionData.GetFullTransaction(this.network);
                IWithdrawal withdrawal = this.withdrawalExtractor.ExtractWithdrawalFromTransaction(walletTrx, transactionData.BlockHash, transactionData.BlockHeight ?? 0);
                if (withdrawal == null)
                    continue;

                yield return withdrawal;
            }
        }

        /// <inheritdoc />
        public List<(Transaction, TransactionData, IWithdrawal)> FindWithdrawalTransactions(uint256 depositId = null)
        {
            lock (this.lockObject)
            {
                List<(Transaction, TransactionData, IWithdrawal)> withdrawals = new List<(Transaction, TransactionData, IWithdrawal)>();

                foreach (TransactionData transactionData in this.Wallet.MultiSigAddress.Transactions)
                {
                    Transaction walletTran = transactionData.GetFullTransaction(this.network);
                    IWithdrawal withdrawal = this.withdrawalExtractor.ExtractWithdrawalFromTransaction(walletTran, transactionData.BlockHash, transactionData.BlockHeight ?? 0);
                    if (withdrawal == null)
                        continue;

                    if (depositId != null && withdrawal.DepositId != depositId)
                        continue;

                    withdrawals.Add((walletTran, transactionData, withdrawal));
                }

                return withdrawals;
            }
        }

        /// <summary>
        /// Checks if a transaction has valid UTXOs that are spent by it.
        /// </summary>
        /// <param name="transaction">The transaction to check.</param>
        /// <param name="coins">Returns the coins found if this parameter supplies an empty coin list.</param>
        /// <returns><c>True</c> if UTXO's are valid and <c>false</c> otherwise.</returns>
        private bool TransactionHasValidUTXOs(Transaction transaction, List<Coin> coins = null)
        {
            lock (this.lockObject)
            {
                // All the input UTXO's should be present in spending details of the multi-sig address.
                foreach (TxIn input in transaction.Inputs)
                {
                    foreach (TransactionData transactionData in this.Wallet.MultiSigAddress.Transactions
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
        }

        /// <inheritdoc />
        public int CompareOutpoints(OutPoint outPoint1, OutPoint outPoint2)
        {
            lock (this.lockObject)
            {
                TransactionData transactionData1 = this.outpointLookup[outPoint1];
                TransactionData transactionData2 = this.outpointLookup[outPoint2];

                return FederationWalletTransactionBuilder.CompareTransactionData(transactionData1, transactionData2);
            }
        }

        /// <inheritdoc />
        public bool ValidateTransaction(Transaction transaction, bool checkSignature = false)
        {
            lock (this.lockObject)
            {
                // All the input UTXO's should be present in spending details of the multi-sig address.
                List<Coin> coins = checkSignature ? new List<Coin>() : null;
                // Verify that the transaction has valid UTXOs.
                if (!this.TransactionHasValidUTXOs(transaction, coins))
                    return false;

                // Verify that there are no earlier unspent UTXOs.
                Comparer<TransactionData> comparer = Comparer<TransactionData>.Create((x, y) => FederationWalletTransactionBuilder.CompareTransactionData(x, y));
                TransactionData earliestUnspent = this.Wallet.MultiSigAddress.Transactions.Where(t => t.SpendingDetails == null).OrderBy(t => t, comparer).FirstOrDefault();
                if (earliestUnspent != null)
                {
                    TransactionData oldestInput = transaction.Inputs.Select(i => this.outpointLookup[i.PrevOut]).OrderByDescending(t => t, comparer).FirstOrDefault();
                    if (oldestInput != null && FederationWalletTransactionBuilder.CompareTransactionData(earliestUnspent, oldestInput) < 0)
                        return false;
                }

                // Verify that all inputs are signed.
                if (checkSignature)
                {
                    TransactionBuilder builder = new TransactionBuilder(this.Wallet.Network).AddCoins(coins);
                    if (!builder.Verify(transaction, this.federationGatewaySettings.TransactionFee, out TransactionPolicyError[] errors))
                    {
                        foreach (TransactionPolicyError transactionPolicyError in errors)
                        {
                            this.logger.LogError("TransactionBuilder.Verify FAILED - {0}", transactionPolicyError.ToString());
                        }

                        return false;
                    }
                }

                return true;
            }
        }

        /// <inheritdoc />
        public void UpdateLastBlockSyncedHeight(ChainedHeader chainedHeader)
        {
            Guard.NotNull(chainedHeader, nameof(chainedHeader));

            lock (this.lockObject)
            {
                // The block locator will help when the wallet
                // needs to rewind this will be used to find the fork.
                this.Wallet.BlockLocator = chainedHeader.GetLocator().Blocks;

                // Update the wallets with the last processed block height.
                this.Wallet.LastBlockSyncedHeight = chainedHeader.Height;
                this.Wallet.LastBlockSyncedHash = chainedHeader.HashBlock;
            }

            this.WalletTipHash = chainedHeader.HashBlock;
        }

        /// <summary>
        /// Generates the wallet file.
        /// </summary>
        /// <returns>The wallet object that was saved into the file system.</returns>
        /// <exception cref="WalletException">Thrown if wallet cannot be created.</exception>
        public FederationWallet GenerateWallet()
        {
            this.logger.LogTrace("Generating the federation wallet file.");

            // Check if any wallet file already exists, with case insensitive comparison.
            if (this.fileStorage.Exists(WalletFileName))
            {
                this.logger.LogTrace("(-)[WALLET_ALREADY_EXISTS]");
                throw new WalletException("A federation wallet already exists.");
            }

            FederationWallet wallet = new FederationWallet
            {
                CreationTime = this.dateTimeProvider.GetTimeOffset(),
                Network = this.network,
                CoinType = this.coinType,
                LastBlockSyncedHeight = 0,
                LastBlockSyncedHash = this.chain.Genesis.HashBlock,
                MultiSigAddress = new MultiSigAddress
                {
                    Address = this.federationGatewaySettings.MultiSigAddress.ToString(),
                    M = this.federationGatewaySettings.MultiSigM,
                    ScriptPubKey = this.federationGatewaySettings.MultiSigAddress.ScriptPubKey,
                    RedeemScript = this.federationGatewaySettings.MultiSigRedeemScript,
                    Transactions = new List<TransactionData>()
                }
            };

            this.logger.LogTrace("(-)");
            return wallet;
        }

        /// <inheritdoc />
        public void EnableFederation(string password, string mnemonic = null, string passphrase = null)
        {
            Guard.NotEmpty(password, nameof(password));

            // Protect against de-activation if the federation is already active.
            if (this.isFederationActive)
            {
                this.logger.LogWarning("(-):[FEDERATION_ALREADY_ACTIVE]");
                return;
            }

            // Get the key and encrypted seed.
            Key key = null;
            string encryptedSeed = this.Wallet.EncryptedSeed;

            if (!string.IsNullOrEmpty(mnemonic))
            {
                ExtKey extendedKey;
                try
                {
                    extendedKey = HdOperations.GetExtendedKey(mnemonic, passphrase);
                }
                catch (NotSupportedException ex)
                {
                    this.logger.LogTrace("Exception occurred: {0}", ex.ToString());
                    this.logger.LogTrace("(-)[EXCEPTION]");

                    if (ex.Message == "Unknown")
                        throw new WalletException("Please make sure you enter valid mnemonic words.");

                    throw;
                }

                // Create a wallet file.
                key = extendedKey.PrivateKey;
                encryptedSeed = key.GetEncryptedBitcoinSecret(password, this.network).ToWif();
            }

            try
            {
                if (key == null)
                    key = Key.Parse(encryptedSeed, password, this.Wallet.Network);

                bool isValidKey = key.PubKey.ToHex() == this.federationGatewaySettings.PublicKey;
                if (!isValidKey)
                {
                    this.logger.LogInformation("The wallet public key {0} does not match the federation member's public key {1}", key.PubKey.ToHex(), this.federationGatewaySettings.PublicKey);
                    return;
                }

                this.Secret = new WalletSecret() { WalletPassword = password };
                this.Wallet.EncryptedSeed = encryptedSeed;
                this.SaveWallet();

                this.isFederationActive = isValidKey;
            }
            catch (Exception ex)
            {
                throw new SecurityException(ex.Message);
            }
        }

        public bool IsFederationActive()
        {
            return this.isFederationActive;
        }

        public FederationWallet GetWallet()
        {
            return this.Wallet;
        }
    }
}
