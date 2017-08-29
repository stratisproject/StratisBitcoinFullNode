﻿using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities.FileStorage;

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
        private readonly Network network;
        private readonly ILogger logger;
        private readonly FileStorage<WatchOnlyWallet> fileStorage;

        public WatchOnlyWalletManager(ILoggerFactory loggerFactory, Network network, DataFolder dataFolder)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.fileStorage = new FileStorage<WatchOnlyWallet>(dataFolder.WalletPath);
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
            var hash = transaction.GetHash();
            this.logger.LogDebug($"watch only wallet received transaction - hash: {hash}, coin: {this.coinType}");

            // Check the transaction outputs for transactions we might be interested in.
            foreach (TxOut utxo in transaction.Outputs)
            {
                // Check if the outputs contain one of our addresses.
                this.Wallet.WatchedAddresses.TryGetValue(utxo.ScriptPubKey.ToString(), out WatchedAddress addressInWallet);

                if (addressInWallet != null)
                {
                    // Retrieve a transaction, if present.
                    string transactionHash = transaction.GetHash().ToString();
                    addressInWallet.Transactions.TryGetValue(transactionHash, out TransactionData existingTransaction);
                    if (existingTransaction == null)
                    {
                        addressInWallet.Transactions.TryAdd(transactionHash, new TransactionData
                        {
                            Hex = transaction.ToHex(),
                            BlockHash = block?.GetHash(),
                        });
                    }
                    else
                    {
                        // If there is a transaction already present, update the hash of the block containing it.
                        existingTransaction.BlockHash = block?.GetHash();
                    }

                    this.SaveWatchOnlyWallet();
                }
            }
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
                CreationTime = DateTimeOffset.Now,
                WatchedAddresses = new ConcurrentDictionary<string, WatchedAddress>()
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
    }
}