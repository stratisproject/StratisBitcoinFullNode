﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
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

        /// <summary>
        /// Provides a rapid lookup of transactions appearing in the watch-only wallet.
        /// This includes both transactions under watched addresses, as well as stored
        /// transactions.
        /// </summary>
        internal Dictionary<uint256, TransactionData> txLookup;

        public WatchOnlyWalletManager(IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, Network network, DataFolder dataFolder)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.fileStorage = new FileStorage<WatchOnlyWallet>(dataFolder.WalletPath);
            this.dateTimeProvider = dateTimeProvider;
        }
        
        /// <inheritdoc />
        public void Dispose()
        {
            this.SaveWatchOnlyWallet();
        }

        /// <inheritdoc />
        public void Initialize()
        {
            // load the watch only wallet into memory
            this.Wallet = this.LoadWatchOnlyWallet();

            LoadTransactionLookup();
        }

        /// <inheritdoc />
        public void WatchAddress(string address)
        {
            var script = BitcoinAddress.Create(address, this.network).ScriptPubKey;

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
            var transactionHash = transaction.GetHash();
            this.logger.LogDebug($"watch only wallet received transaction - hash: {transactionHash}, coin: {this.coinType}");

            // Check the transaction inputs to see if a watched address is affected.
            foreach (TxIn input in transaction.Inputs)
            {
                // See if the previous transaction is in the watch-only wallet.
                this.txLookup.TryGetValue(input.PrevOut.Hash, out TransactionData prevTransaction);

                // If it is null, it can't be related to one of the watched addresses (or it is the very first watched transaction)
                if (prevTransaction == null)
                    continue;

                // Check if the previous transaction's outputs contain one of our addresses.
                foreach (TxOut prevOutput in prevTransaction.Transaction.Outputs)
                {
                    this.Wallet.WatchedAddresses.TryGetValue(prevOutput.ScriptPubKey.ToString(), out WatchedAddress addressInWallet);

                    if (addressInWallet != null)
                    {
                        // Retrieve a transaction, if present.
                        addressInWallet.Transactions.TryGetValue(transactionHash.ToString(), out TransactionData existingTransaction);

                        if (existingTransaction == null)
                        {
                            TransactionData newTransaction = new TransactionData
                            {
                                Id = transactionHash,
                                Hex = transaction.ToHex(),
                                BlockHash = block?.GetHash(),
                            };

                            // Add the Merkle proof to the transaction.
                            if (block != null)
                            {
                                newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                            }

                            addressInWallet.Transactions.TryAdd(transactionHash.ToString(), newTransaction);

                            // Update the lookup cache with the new transaction information.
                            this.txLookup.Remove(newTransaction.Id);
                            this.txLookup.Add(newTransaction.Id, newTransaction);
                        }
                        else
                        {
                            // If there is a transaction already present, update the hash of the block containing it.
                            existingTransaction.BlockHash = block?.GetHash();

                            // Add the Merkle proof now that the transaction is confirmed in a block.
                            if (block != null && existingTransaction.MerkleProof == null)
                            {
                                existingTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                            }

                            // Update the lookup cache with the new transaction information.
                            this.txLookup.Remove(existingTransaction.Id);
                            this.txLookup.Add(existingTransaction.Id, existingTransaction);
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
                        TransactionData newTransaction = new TransactionData
                        {
                            Id = transactionHash,
                            Hex = transaction.ToHex(),
                            BlockHash = block?.GetHash(),
                        };

                        // Add the Merkle proof to the (non-spending) transaction.
                        if (block != null)
                        {
                            newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                        }

                        addressInWallet.Transactions.TryAdd(transactionHash.ToString(), newTransaction);

                        // Update the lookup cache with the new transaction information.
                        this.txLookup.Remove(newTransaction.Id);
                        this.txLookup.Add(newTransaction.Id, newTransaction);
                    }
                    else
                    {
                        // If there is a transaction already present, update the hash of the block containing it.
                        existingTransaction.BlockHash = block?.GetHash();

                        // Add the Merkle proof now that the transaction is confirmed in a block.
                        if (block != null && existingTransaction.MerkleProof == null)
                        {
                            existingTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                        }

                        // Update the lookup cache with the new transaction information.
                        this.txLookup.Remove(existingTransaction.Id);
                        this.txLookup.Add(existingTransaction.Id, existingTransaction);
                    }

                    this.SaveWatchOnlyWallet();
                }
            }

            this.Wallet.WatchedTransactions.TryGetValue(transactionHash.ToString(), out TransactionData existingWatchedTransaction);

            if (existingWatchedTransaction != null)
            {
                // The transaction was previously stored, in an unconfirmed state.
                // So now update the block hash and Merkle proof since it has
                // appeared in a block.
                existingWatchedTransaction.BlockHash = block.GetHash();
                existingWatchedTransaction.MerkleProof = new MerkleBlock(block, new[] {transaction.GetHash()}).PartialMerkleTree;

                // Update the lookup cache with the new transaction information.
                this.txLookup.Remove(existingWatchedTransaction.Id);
                this.txLookup.Add(existingWatchedTransaction.Id, existingWatchedTransaction);

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
            var lookup = new Dictionary<uint256, TransactionData>();
            var watchOnlyWallet = this.GetWatchOnlyWallet();

            foreach (var address in watchOnlyWallet.WatchedAddresses.Values)
            {
                foreach (var transaction in address.Transactions.Values)
                {
                    lookup.Add(transaction.Id, transaction);
                }
            }

            foreach (var transaction in watchOnlyWallet.WatchedTransactions.Values)
            {
                // It is conceivable that a transaction could be both watched
                // in isolation and watched as a transaction under one or
                // more watched addresses.
                if (!lookup.TryAdd(transaction.Id, transaction))
                {
                    // Check to see if there is better information in
                    // the watched transaction than the watched address.
                    // If there is, use the watched transaction instead.

                    var existingTx = lookup[transaction.Id];

                    if ((existingTx.MerkleProof == null && transaction.MerkleProof != null) ||
                        (existingTx.BlockHash == null && transaction.BlockHash != null))
                    {
                        lookup.Remove(transaction.Id);
                        lookup.Add(transaction.Id, transaction);
                    }
                }
            }

            this.txLookup = lookup;
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

            WatchOnlyWallet watchOnlyWallet = new WatchOnlyWallet
            {
                Network = this.network,
                CoinType = this.coinType,
                CreationTime = this.dateTimeProvider.GetTimeOffset(),
                WatchedAddresses = new ConcurrentDictionary<string, WatchedAddress>(),
                WatchedTransactions = new ConcurrentDictionary<string, TransactionData>()
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
            var scriptToCheck = BitcoinAddress.Create(address, this.network).ScriptPubKey;

            Money balance = new Money(0);

            if (!this.Wallet.WatchedAddresses.ContainsKey(scriptToCheck.ToString()))
                // Returning zero would be misleading.
                return null;

            foreach (var transaction in this.Wallet.WatchedAddresses[scriptToCheck.ToString()].Transactions.Values)
            {
                foreach (TxIn input in transaction.Transaction.Inputs)
                {
                    // See if the previous transaction is in the watch-only wallet.
                    this.txLookup.TryGetValue(input.PrevOut.Hash, out TransactionData prevTransaction);

                    // If it is null, it can't be related to the watched addresses (or it is the very first watched transaction)
                    if (prevTransaction == null)
                        continue;

                    // A sanity check to ensure the referenced output affects the desired address
                    if (prevTransaction.Transaction.Outputs[input.PrevOut.N].ScriptPubKey == scriptToCheck)
                    {
                        // Input = funds are being paid 'out of' the address in question

                        // Computing the input value is not as straightforward as with an output, as the value is not directly
                        // stored in a TxIn object. We need to check the previous output being spent by the input to get this
                        // information. But even an OutPoint does not contain the Value - we need to check the other transactions
                        // in the watch-only wallet to see if we have the prior transaction being referenced.

                        // This does imply that the earliest transaction in the watch-only wallet (for this address) will not
                        // have full previous transaction information stored. Therefore we can only reason about the address
                        // balance after a given block height; any prior transactions are ignored.

                        balance -= prevTransaction.Transaction.Outputs[input.PrevOut.N].Value;
                    }
                }

                // Check if the outputs contain the watched address
                foreach (var output in transaction.Transaction.Outputs)
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
