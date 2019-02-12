using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.WatchOnlyWallet
{
    /// <summary>
    /// Class representing a manager for a watch-only wallet.
    /// In this implementation, the wallet is saved to the file system.
    /// </summary>
    public class WatchOnlyWalletManager : IWatchOnlyWalletManager
    {
        /// <summary>
        /// The name of the watch-only wallet as saved in the file system.
        /// </summary>
        private const string WalletFileName = "watch_only_wallet.json";

        /// <summary>
        /// A wallet containing scripts that are monitored for transactions affecting them.
        /// </summary>
        public WatchOnlyWallet Wallet { get; private set; }

        private readonly CoinType coinType;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly FileStorage<WatchOnlyWallet> fileStorage;

        /// <summary>Provider of date time functionality.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        private readonly ISignals signals;

        /// <summary>
        /// Provides a rapid lookup of transactions appearing in the watch-only wallet.
        /// This includes both transactions under watched addresses, as well as stored
        /// transactions. Enables quicker computation of address balances etc.
        /// </summary>
        private ConcurrentDictionary<uint256, TransactionData> txLookup;

        public WatchOnlyWalletManager(IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, Network network, DataFolder dataFolder, ISignals signals)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.fileStorage = new FileStorage<WatchOnlyWallet>(dataFolder.WalletPath);
            this.dateTimeProvider = dateTimeProvider;
            this.signals = signals;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.signals.OnBlockConnected.Detach(this.OnBlockConnected);
            this.signals.OnTransactionReceived.Detach(this.OnTransactionAvailable);

            this.SaveWatchOnlyWallet();
        }

        /// <inheritdoc />
        public void Initialize()
        {
            // load the watch only wallet into memory
            this.Wallet = this.LoadWatchOnlyWallet();

            this.signals.OnBlockConnected.Attach(this.OnBlockConnected);
            this.signals.OnTransactionReceived.Attach(this.OnTransactionAvailable);

            this.LoadTransactionLookup();
        }

        private void OnTransactionAvailable(Transaction transaction)
        {
            this.ProcessTransaction(transaction);
        }

        private void OnBlockConnected(ChainedHeaderBlock chainedheaderblock)
        {
            this.ProcessBlock(chainedheaderblock.Block);
        }

        /// <inheritdoc />
        public void WatchAddress(string address)
        {
            Script script = BitcoinAddress.Create(address, this.network).ScriptPubKey;

            if (this.Wallet.WatchedAddresses.ContainsKey(script.ToString()))
            {
                this.logger.LogDebug($"already watching script: {script}. coin: {this.coinType}");
                return;
            }

            this.logger.LogDebug($"added script: {script} to the watch list. coin: {this.coinType}");
            this.Wallet.WatchedAddresses.TryAdd(script.ToString(), new WatchedAddress
            {
                Script = script,
                Address = address
            });

            this.SaveWatchOnlyWallet();
        }

        /// <inheritdoc />
        public void StoreTransaction(TransactionData transactionData)
        {
            if (this.Wallet.WatchedTransactions.ContainsKey(transactionData.Id.ToString()))
            {
                this.logger.LogDebug($"already watching transaction: {transactionData.Id}. coin: {this.coinType}");
                return;
            }

            this.logger.LogDebug($"added transaction: {transactionData.Id} to the watch list. coin: {this.coinType}");
            this.Wallet.WatchedTransactions.TryAdd(transactionData.Id.ToString(), new TransactionData
            {
                BlockHash = transactionData.BlockHash,
                Hex = transactionData.Hex,
                Id = transactionData.Id,
                MerkleProof = transactionData.MerkleProof
            });

            this.SaveWatchOnlyWallet();
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block)
        {
            this.logger.LogDebug($"Watch only wallet received block with hash: {block.Header.GetHash()}, coin: {this.coinType}");

            foreach (Transaction transaction in block.Transactions)
            {
                this.ProcessTransaction(transaction, block);
            }
        }

        /// <inheritdoc />
        public void ProcessTransaction(Transaction transaction, Block block = null)
        {
            uint256 transactionHash = transaction.GetHash();
            this.logger.LogDebug($"watch only wallet received transaction - hash: {transactionHash}, coin: {this.coinType}");

            // Check the transaction inputs to see if a watched address is affected.
            foreach (TxIn input in transaction.Inputs)
            {
                // See if the previous transaction is in the watch-only wallet.
                this.txLookup.TryGetValue(input.PrevOut.Hash, out TransactionData prevTransactionData);

                // If it is null, it can't be related to one of the watched addresses (or it is the very first watched transaction)
                if (prevTransactionData == null)
                    continue;

                var prevTransaction = this.network.CreateTransaction(prevTransactionData.Hex);

                // Check if the previous transaction's outputs contain one of our addresses.
                foreach (TxOut prevOutput in prevTransaction.Outputs)
                {
                    this.Wallet.WatchedAddresses.TryGetValue(prevOutput.ScriptPubKey.ToString(), out WatchedAddress addressInWallet);

                    if (addressInWallet != null)
                    {
                        // Retrieve a transaction, if present.
                        addressInWallet.Transactions.TryGetValue(transactionHash.ToString(), out TransactionData existingTransaction);

                        if (existingTransaction == null)
                        {
                            var newTransaction = new TransactionData
                            {
                                Id = transactionHash,
                                Hex = transaction.ToHex(),
                                BlockHash = block?.GetHash()
                            };

                            // Add the Merkle proof to the transaction.
                            if (block != null)
                            {
                                newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                            }

                            addressInWallet.Transactions.TryAdd(transactionHash.ToString(), newTransaction);

                            // Update the lookup cache with the new transaction information.
                            // Since the WO record is new it probably isn't in the lookup cache.
                            this.txLookup.TryAdd(newTransaction.Id, newTransaction);
                        }
                        else
                        {
                            // If there was a transaction already present in the WO wallet,
                            // it is most likely that it has now been confirmed in a block.
                            // Therefore, update the transaction record with the hash of the
                            // block containing the transaction.
                            if (existingTransaction.BlockHash == null)
                                existingTransaction.BlockHash = block?.GetHash();

                            if (block != null && existingTransaction.MerkleProof == null)
                            {
                                // Add the Merkle proof now that the transaction is confirmed in a block.
                                existingTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                            }

                            // Update the lookup cache with the new transaction information.
                            // Since the WO record was not new it probably is already in the lookup cache.
                            // Therefore, unconditionally update it.
                            this.txLookup.AddOrUpdate(existingTransaction.Id, existingTransaction, (key, oldValue) => existingTransaction);
                        }

                        this.SaveWatchOnlyWallet();
                    }
                }
            }

            // Check the transaction outputs for transactions we might be interested in.
            foreach (TxOut utxo in transaction.Outputs)
            {
                // Check if the outputs contain one of our addresses.
                this.Wallet.WatchedAddresses.TryGetValue(utxo.ScriptPubKey.ToString(), out WatchedAddress addressInWallet);

                if (addressInWallet != null)
                {
                    // Retrieve a transaction, if present.
                    addressInWallet.Transactions.TryGetValue(transactionHash.ToString(), out TransactionData existingTransaction);

                    if (existingTransaction == null)
                    {
                        var newTransaction = new TransactionData
                        {
                            Id = transactionHash,
                            Hex = transaction.ToHex(),
                            BlockHash = block?.GetHash()
                        };

                        // Add the Merkle proof to the (non-spending) transaction.
                        if (block != null)
                        {
                            newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                        }

                        addressInWallet.Transactions.TryAdd(transactionHash.ToString(), newTransaction);

                        // Update the lookup cache with the new transaction information.
                        // Since the WO record is new it probably isn't in the lookup cache.
                        this.txLookup.TryAdd(newTransaction.Id, newTransaction);
                    }
                    else
                    {
                        // If there was a transaction already present in the WO wallet,
                        // it is most likely that it has now been confirmed in a block.
                        // Therefore, update the transaction record with the hash of the
                        // block containing the transaction.
                        if (existingTransaction.BlockHash == null)
                            existingTransaction.BlockHash = block?.GetHash();

                        if (block != null && existingTransaction.MerkleProof == null)
                        {
                            // Add the Merkle proof now that the transaction is confirmed in a block.
                            existingTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                        }

                        // Update the lookup cache with the new transaction information.
                        // Since the WO record was not new it probably is already in the lookup cache.
                        // Therefore, unconditionally update it.
                        this.txLookup.AddOrUpdate(existingTransaction.Id, existingTransaction, (key, oldValue) => existingTransaction);
                    }

                    this.SaveWatchOnlyWallet();
                }
            }

            this.Wallet.WatchedTransactions.TryGetValue(transactionHash.ToString(), out TransactionData existingWatchedTransaction);

            if (existingWatchedTransaction != null && block != null)
            {
                // The transaction was previously stored, in an unconfirmed state.
                // So now update the block hash and Merkle proof since it has
                // appeared in a block.
                existingWatchedTransaction.BlockHash = block.GetHash();
                existingWatchedTransaction.MerkleProof = new MerkleBlock(block, new[] {transaction.GetHash()}).PartialMerkleTree;

                // Update the lookup cache with the new transaction information.
                this.txLookup.AddOrUpdate(existingWatchedTransaction.Id, existingWatchedTransaction, (key, oldValue) => existingWatchedTransaction);

                this.SaveWatchOnlyWallet();
            }
        }

        /// <summary>
        /// Populate the transaction lookup dictionary with the current
        /// contents of the watch only wallet's watched addresses and
        /// transactions.
        /// </summary>
        private void LoadTransactionLookup()
        {
            this.txLookup = this.Wallet.GetWatchedTransactions();
        }

        /// <inheritdoc />
        public void SaveWatchOnlyWallet()
        {
            this.fileStorage.SaveToFile(this.Wallet, WalletFileName);
        }

        /// <inheritdoc />
        public WatchOnlyWallet LoadWatchOnlyWallet()
        {
            if (this.fileStorage.Exists(WalletFileName))
            {
                return this.fileStorage.LoadByFileName(WalletFileName);
            }

            var watchOnlyWallet = new WatchOnlyWallet
            {
                Network = this.network,
                CoinType = this.coinType,
                CreationTime = this.dateTimeProvider.GetTimeOffset()
            };

            this.fileStorage.SaveToFile(watchOnlyWallet, WalletFileName);
            return watchOnlyWallet;
        }

        /// <summary>
        /// Gets the watch-only wallet.
        /// </summary>
        /// <returns>The watch-only wallet.</returns>
        public WatchOnlyWallet GetWatchOnlyWallet()
        {
            return this.Wallet;
        }

        /// <inheritdoc />
        /// <remarks>
        /// TODO ideally we'd have WatchAddress call this method, but the value populating the Address field is slightly different.
        /// The Address field is actually not used anywhere and is more there for info.
        /// Regardless, we need to consolidate them.
        /// </remarks>
        public void WatchScriptPubKey(Script scriptPubKey)
        {
            if (this.Wallet.WatchedAddresses.ContainsKey(scriptPubKey.ToString()))
            {
                this.logger.LogDebug($"already watching script: {scriptPubKey}. coin: {this.coinType}");
                return;
            }

            this.logger.LogDebug($"added script: {scriptPubKey} to the watch list. coin: {this.coinType}");
            this.Wallet.WatchedAddresses.TryAdd(scriptPubKey.ToString(), new WatchedAddress
            {
                Script = scriptPubKey,
                Address = scriptPubKey.Hash.ToString()
            });

            this.SaveWatchOnlyWallet();
        }

        /// <summary>
        /// Computes the value contained within the transactions currently
        /// being watched for the given address by the watch-only wallet.
        /// For high-precision applications use a block explorer instead,
        /// as the returned balance only reflects fund movement since the
        /// address first started being watched.
        /// </summary>
        /// <param name="address">The Base58 representation of the address to interrogate</param>
        public Money GetRelativeBalance(string address)
        {
            Script scriptToCheck = BitcoinAddress.Create(address, this.network).ScriptPubKey;

            var balance = new Money(0);

            if (!this.Wallet.WatchedAddresses.ContainsKey(scriptToCheck.ToString()))
                // Returning zero would be misleading.
                return null;

            foreach (TransactionData transactionData in this.Wallet.WatchedAddresses[scriptToCheck.ToString()].Transactions.Values)
            {
                var transaction = this.network.CreateTransaction(transactionData.Hex);

                foreach (TxIn input in transaction.Inputs)
                {
                    // See if the previous transaction is in the watch-only wallet.
                    this.txLookup.TryGetValue(input.PrevOut.Hash, out TransactionData prevTransactionData);

                    // If it is null, it can't be related to the watched addresses (or it is the very first watched transaction)
                    if (prevTransactionData == null)
                        continue;

                    var prevTransaction = this.network.CreateTransaction(prevTransactionData.Hex);

                    // A sanity check to ensure the referenced output affects the desired address
                    if (prevTransaction.Outputs[input.PrevOut.N].ScriptPubKey == scriptToCheck)
                    {
                        // Input = funds are being paid 'out of' the address in question

                        // Computing the input value is not as straightforward as with an output, as the value is not directly
                        // stored in a TxIn object. We need to check the previous output being spent by the input to get this
                        // information. But even an OutPoint does not contain the Value - we need to check the other transactions
                        // in the watch-only wallet to see if we have the prior transaction being referenced.

                        // This does imply that the earliest transaction in the watch-only wallet (for this address) will not
                        // have full previous transaction information stored. Therefore we can only reason about the address
                        // balance after a given block height; any prior transactions are ignored.

                        balance -= prevTransaction.Outputs[input.PrevOut.N].Value;
                    }
                }

                // Check if the outputs contain the watched address
                foreach (TxOut output in transaction.Outputs)
                {
                    if (output.ScriptPubKey == scriptToCheck)
                    {
                        // Output = funds are being paid 'into' the address in question
                        balance += output.Value;
                    }
                }
            }

            return balance;
        }
    }
}
