using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Features.Wallet;
using Script = NBitcoin.Script;

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
        /// The watch-only wallet this manager manages.
        /// </summary>
        public WatchOnlyWallet Wallet { get; private set; }

        private readonly CoinType coinType;
        private readonly Network network;
        private readonly IConnectionManager connectionManager;
        private readonly ConcurrentChain chain;
        private readonly NodeSettings settings;
        private readonly DataFolder dataFolder;
        private readonly ILogger logger;

        public WatchOnlyWalletManager(ILoggerFactory loggerFactory, IConnectionManager connectionManager, Network network, ConcurrentChain chain, NodeSettings settings, DataFolder dataFolder)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.connectionManager = connectionManager;
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chain = chain;
            this.settings = settings;
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
        public uint256 LastReceivedBlock { get; }

        /// <inheritdoc />
        public void RemoveBlocks(ChainedBlock fork)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Watch(Script script)
        {
            if (this.Wallet.Scripts.Contains(script))
            {
                this.logger.LogDebug($"already watching script: {script}. coin: {this.coinType}");
                return;
            }

            this.logger.LogDebug($"added script: {script} to the watch list. coin: {this.coinType}");
            this.Wallet.Scripts.Add(script);
            this.SaveWatchOnlyWallet();
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block)
        {
            ChainedBlock chainedBlock = this.chain.GetBlock(block.GetHash());
            this.logger.LogDebug($"watch only wallet received block height: {chainedBlock.Height}, hash: {block.Header.GetHash()}, coin: {this.coinType}");

            foreach (Transaction transaction in block.Transactions)
            {
                this.ProcessTransaction(transaction, chainedBlock.Height, block);
            }
        }

        /// <inheritdoc />
        public void ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null)
        {
            var hash = transaction.GetHash();
            this.logger.LogDebug($"watch only wallet received transaction - hash: {hash}, coin: {this.coinType}");

            // check the outputs
            foreach (TxOut utxo in transaction.Outputs)
            {
                TransactionVerboseModel model = new TransactionVerboseModel(transaction, this.network);

                // check if the outputs contain one of our addresses
                if (this.Wallet.Scripts.Contains(utxo.ScriptPubKey) && this.Wallet.Transactions.All(t => t.hex != model.hex))
                {
                    this.Wallet.Transactions.Add(model);
                    this.SaveWatchOnlyWallet();
                }
            }
        }

        /// <inheritdoc />
        public void UpdateLastBlockSyncedHeight(ChainedBlock chainedBlock)
        {
            throw new NotImplementedException();
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
                    Scripts = new List<Script>(),
                    Transactions = new List<TransactionVerboseModel>()
                };

                this.SaveWatchOnlyWallet();
            }

            // load the file from the local system
            return JsonConvert.DeserializeObject<WatchOnlyWallet>(File.ReadAllText(walletFilePath));
        }

        /// <summary>
        /// Gets the watch-only wallet.
        /// </summary>
        /// <returns>The watch-only wallet.</returns>
        public WatchOnlyWallet GetWallet()
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
