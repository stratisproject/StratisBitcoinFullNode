using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;

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
        private readonly DataFolder dataFolder;
        private readonly ILogger logger;

        public WatchOnlyWalletManager(ILoggerFactory loggerFactory, Network network, ConcurrentChain chain, DataFolder dataFolder)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.dataFolder = dataFolder;
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

            if (this.Wallet.WatchedAddresses.Any(wa => wa.Script == script))
            {
                this.logger.LogDebug($"already watching script: {script}. coin: {this.coinType}");
                return;
            }

            this.logger.LogDebug($"added script: {script} to the watch list. coin: {this.coinType}");
            this.Wallet.WatchedAddresses.Add(new WatchedAddress
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
                WatchedAddress addressInWallet = this.Wallet.WatchedAddresses.SingleOrDefault(wa => wa.Script == utxo.ScriptPubKey);

                if (addressInWallet != null)
                {
                    // Retrieve a transaction, if present.
                    var existingTransaction = addressInWallet.Transactions.SingleOrDefault(t => t.Hex == transaction.ToHex());
                    if (existingTransaction == null)
                    {
                        addressInWallet.Transactions.Add(new TransactionData
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
            File.WriteAllText(this.GetWalletFilePath(), JsonConvert.SerializeObject(this.Wallet, Formatting.Indented));
        }

        /// <inheritdoc />
        public WatchOnlyWallet LoadWatchOnlyWallet()
        {
            string walletFilePath = this.GetWalletFilePath();
            if (!File.Exists(walletFilePath))
            {
                this.Wallet = new WatchOnlyWallet
                {
                    Network = this.network,
                    CoinType = this.coinType,
                    CreationTime = DateTimeOffset.Now,
                    WatchedAddresses = new List<WatchedAddress>()
                };

                this.SaveWatchOnlyWallet();
            }

            // Load the file from the local system.
            return JsonConvert.DeserializeObject<WatchOnlyWallet>(File.ReadAllText(walletFilePath));
        }

        /// <summary>
        /// Gets the watch-only wallet.
        /// </summary>
        /// <returns>The watch-only wallet.</returns>
        public WatchOnlyWallet GetWatchOnlyWallet()
        {
            return this.Wallet;
        }

        /// <summary>
        /// Gets the file path where the watch-only wallet is saved.
        /// </summary>
        /// <returns>The watch-only wallet path in the file system.</returns>
        private string GetWalletFilePath()
        {
            return Path.Combine(this.dataFolder.WalletPath, WalletFileName);
        }
    }
}