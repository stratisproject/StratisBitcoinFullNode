//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using Microsoft.Extensions.Logging;
//using NBitcoin;
//using Stratis.Bitcoin.Features.BlockStore;
//using Stratis.Bitcoin.Features.Notifications.Interfaces;
//using Stratis.Bitcoin.Features.Wallet;
//using Stratis.Bitcoin.Features.WatchOnlyWallet;
//using Stratis.Bitcoin.Interfaces;
//using Stratis.Bitcoin.Utilities;

//namespace City.Chain.Features.SimpleWallet
//{
//    public class SimpleWalletManager : ISimpleWalletManager
//    {
//        private string name;

//        private string version;

//        private WatchOnlyWallet wallet;

//        protected ChainedHeader walletTip;

//        /// <summary>The async loop we need to wait upon before we can shut down this manager.</summary>
//        private IAsyncLoop asyncLoop;

//        /// <summary>Factory for creating background async loop tasks.</summary>
//        private readonly IAsyncLoopFactory asyncLoopFactory;

//        // TODO: Dependency Injection.
//        IDateTimeProvider dateTimeProvider = new DateTimeProvider();

//        private readonly IBlockNotification blockNotification;

//        /// <summary>An interface for getting blocks asynchronously from the blockstore cache.</summary>
//        public readonly IBlockStore blockStoreCache;

//        private readonly Network network;

//        private readonly ConcurrentChain chain;

//        /// <summary>Instance logger.</summary>
//        private readonly ILogger logger;

//        private readonly INodeLifetime nodeLifetime;

//        private readonly CoinType coinType;

//        private readonly HubCommands hubCommands;

//        /// <summary>
//        /// Provides a rapid lookup of transactions appearing in the watch-only wallet.
//        /// This includes both transactions under watched addresses, as well as stored
//        /// transactions. Enables quicker computation of address balances etc.
//        /// </summary>
//        private ConcurrentDictionary<uint256, Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData> txLookup;

//        public SimpleWalletManager(
//            ILoggerFactory loggerFactory,
//            ConcurrentChain chain,
//            IBlockStore blockStoreCache,
//            INodeLifetime nodeLifetime,
//            HubCommands hubCommands,
//            IBlockNotification blockNotification)
//        {
//            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
//            this.network = chain.Network;
//            this.chain = chain;
//            this.blockNotification = blockNotification;
//            this.nodeLifetime = nodeLifetime;
//            this.blockStoreCache = blockStoreCache;
//            this.hubCommands = hubCommands;

//            this.coinType = (CoinType)this.network.Consensus.CoinType;
//        }

//        public void Configure(string name,
//            string version,
//            DateTimeOffset? created)
//        {
//            this.name = name;
//            this.version = version;

//            // If no value is supplied, we will use the current time.
//            if (!created.HasValue)
//            {
//                created = this.dateTimeProvider.GetTimeOffset();
//            }

//            var watchOnlyWallet = new WatchOnlyWallet
//            {
//                Network = this.network,
//                CoinType = CoinType.Stratis,
//                CreationTime = created.Value
//            };

//            this.wallet = watchOnlyWallet;
//        }

//        public WatchOnlyWallet GetWatchOnlyWallet()
//        {
//            return this.wallet;
//        }

//        /// <inheritdoc />
//        public void Initialize()
//        {
//            this.LoadTransactionLookup();

//            // start syncing the wallet from the creation date
//            this.SyncFromDate(this.wallet.CreationTime.DateTime);
//        }

//        public void Dispose()
//        {

//        }

//        /// <summary>
//        /// Populate the transaction lookup dictionary with the current
//        /// contents of the watch only wallet's watched addresses and
//        /// transactions.
//        /// </summary>
//        private void LoadTransactionLookup()
//        {
//            this.txLookup = this.wallet.GetWatchedTransactions();
//        }

//        public void ProcessAndNotify(Transaction transaction, Block block)
//        {
//            this.logger.LogDebug($"SimpleWallet received block with hash: {block.Header.GetHash()}, coin: {this.coinType}");

//            var txDict = new ConcurrentDictionary<uint256, Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData>();
//            var transactionList = new ConcurrentDictionary<uint256, Transaction>();

//            if (transaction != null)
//            {
//                this.ProcessTransaction(txDict, transactionList, transaction, block);
//            }
//            else
//            {
//                foreach (Transaction tx in block.Transactions)
//                {
//                    this.ProcessTransaction(txDict, transactionList, tx, block);
//                }
//            }

//            if (txDict.Count > 0)
//            {
//                //foreach (var item in txDict)
//                //{
//                //    var tx = Transaction.Parse(item.Value.Hex, RawFormat.Satoshi, this.network);
//                //    var txtext = tx.ToString(this.network, RawFormat.BlockExplorer);
//                //    this.hubCommands.SendToUser(this.name, "txs", txtext);
//                //}

//                //this.hubCommands.SendTransactionsToUser(this.name, txDict);
//            }

//            if (transactionList.Count > 0)
//            {
//                IEnumerable<object> jsonTxs = transactionList.Select(t => Newtonsoft.Json.JsonConvert.DeserializeObject(t.Value.ToString(this.network, RawFormat.BlockExplorer)));

//                //foreach (var tx in transactionList)
//                //{
//                //    var txText = tx.Value.ToString(this.network, RawFormat.BlockExplorer);
//                //}

//                this.hubCommands.SendToUser(this.name, "txs", jsonTxs);
//            }
//        }

//        public void ProcessBlock(Block block)
//        {
//            ProcessAndNotify(null, block);
//        }

//        /// <inheritdoc />
//        /// <remarks>
//        /// TODO ideally we'd have WatchAddress call this method, but the value populating the Address field is slightly different.
//        /// The Address field is actually not used anywhere and is more there for info.
//        /// Regardless, we need to consolidate them.
//        /// </remarks>
//        public void WatchScriptPubKey(Script scriptPubKey)
//        {
//            if (this.wallet.WatchedAddresses.ContainsKey(scriptPubKey.ToString()))
//            {
//                this.logger.LogDebug($"already watching script: {scriptPubKey}. coin: {this.coinType}");
//                return;
//            }

//            this.logger.LogDebug($"added script: {scriptPubKey} to the watch list. coin: {this.coinType}");
//            this.wallet.WatchedAddresses.TryAdd(scriptPubKey.ToString(), new WatchedAddress
//            {
//                Script = scriptPubKey,
//                Address = scriptPubKey.Hash.ToString()
//            });

//            //this.SaveWatchOnlyWallet();
//        }

//        /// <inheritdoc />
//        public void StoreTransaction(Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData transactionData)
//        {
//            if (this.wallet.WatchedTransactions.ContainsKey(transactionData.Id.ToString()))
//            {
//                this.logger.LogDebug($"already watching transaction: {transactionData.Id}. coin: {this.coinType}");
//                return;
//            }

//            this.logger.LogDebug($"added transaction: {transactionData.Id} to the watch list. coin: {this.coinType}");
//            this.wallet.WatchedTransactions.TryAdd(transactionData.Id.ToString(), new Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData
//            {
//                BlockHash = transactionData.BlockHash,
//                Hex = transactionData.Hex,
//                Id = transactionData.Id,
//                MerkleProof = transactionData.MerkleProof
//            });

//            //this.SaveWatchOnlyWallet();
//        }

//        /// <inheritdoc />
//        public void ProcessTransaction(ConcurrentDictionary<uint256, Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData> txs, ConcurrentDictionary<uint256, Transaction> transactions, Transaction transaction, Block block = null)
//        {
//            uint256 transactionHash = transaction.GetHash();
//            //this.logger.LogDebug($"watch only wallet received transaction - hash: {transactionHash}, coin: {this.coinType}");

//            // Check the transaction inputs to see if a watched address is affected.
//            foreach (TxIn input in transaction.Inputs)
//            {
//                // See if the previous transaction is in the watch-only wallet.
//                txs.TryGetValue(input.PrevOut.Hash, out Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData prevTransactionData);

//                // If it is null, it can't be related to one of the watched addresses (or it is the very first watched transaction)
//                if (prevTransactionData == null)
//                    continue;

//                var prevTransaction = this.network.CreateTransaction(prevTransactionData.Hex);

//                // Check if the previous transaction's outputs contain one of our addresses.
//                foreach (TxOut prevOutput in prevTransaction.Outputs)
//                {
//                    this.wallet.WatchedAddresses.TryGetValue(prevOutput.ScriptPubKey.ToString(), out WatchedAddress addressInWallet);

//                    if (addressInWallet != null)
//                    {
//                        // Retrieve a transaction, if present.
//                        addressInWallet.Transactions.TryGetValue(transactionHash.ToString(), out Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData existingTransaction);

//                        if (existingTransaction == null)
//                        {
//                            var newTransaction = new Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData
//                            {
//                                Id = transactionHash,
//                                Hex = transaction.ToHex(),
//                                BlockHash = block?.GetHash()
//                            };

//                            // Add the Merkle proof to the transaction.
//                            if (block != null)
//                            {
//                                newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
//                            }

//                            addressInWallet.Transactions.TryAdd(transactionHash.ToString(), newTransaction);

//                            transactions.TryAdd(newTransaction.Id, transaction);

//                            // Update the lookup cache with the new transaction information.
//                            // Since the WO record is new it probably isn't in the lookup cache.
//                            txs.TryAdd(newTransaction.Id, newTransaction);
//                        }
//                        else
//                        {
//                            // If there was a transaction already present in the WO wallet,
//                            // it is most likely that it has now been confirmed in a block.
//                            // Therefore, update the transaction record with the hash of the
//                            // block containing the transaction.
//                            if (existingTransaction.BlockHash == null)
//                                existingTransaction.BlockHash = block?.GetHash();

//                            if (block != null && existingTransaction.MerkleProof == null)
//                            {
//                                // Add the Merkle proof now that the transaction is confirmed in a block.
//                                existingTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
//                            }

//                            // Update the lookup cache with the new transaction information.
//                            // Since the WO record was not new it probably is already in the lookup cache.
//                            // Therefore, unconditionally update it.
//                            txs.AddOrUpdate(existingTransaction.Id, existingTransaction, (key, oldValue) => existingTransaction);

//                            transactions.AddOrUpdate(existingTransaction.Id, transaction, (key, oldValue) => transaction);
//                        }

//                        //this.SaveWatchOnlyWallet();
//                    }
//                }
//            }

//            // Check the transaction outputs for transactions we might be interested in.
//            foreach (TxOut utxo in transaction.Outputs)
//            {
//                // Check if the outputs contain one of our addresses.
//                this.wallet.WatchedAddresses.TryGetValue(utxo.ScriptPubKey.ToString(), out WatchedAddress addressInWallet);

//                if (addressInWallet != null)
//                {
//                    // Retrieve a transaction, if present.
//                    addressInWallet.Transactions.TryGetValue(transactionHash.ToString(), out Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData existingTransaction);

//                    if (existingTransaction == null)
//                    {
//                        var newTransaction = new Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData
//                        {
//                            Id = transactionHash,
//                            Hex = transaction.ToHex(),
//                            BlockHash = block?.GetHash()
//                        };

//                        // Add the Merkle proof to the (non-spending) transaction.
//                        if (block != null)
//                        {
//                            newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
//                        }

//                        addressInWallet.Transactions.TryAdd(transactionHash.ToString(), newTransaction);

//                        // Update the lookup cache with the new transaction information.
//                        // Since the WO record is new it probably isn't in the lookup cache.
//                        txs.TryAdd(newTransaction.Id, newTransaction);

//                        transactions.TryAdd(newTransaction.Id, transaction);
//                    }
//                    else
//                    {
//                        // If there was a transaction already present in the WO wallet,
//                        // it is most likely that it has now been confirmed in a block.
//                        // Therefore, update the transaction record with the hash of the
//                        // block containing the transaction.
//                        if (existingTransaction.BlockHash == null)
//                            existingTransaction.BlockHash = block?.GetHash();

//                        if (block != null && existingTransaction.MerkleProof == null)
//                        {
//                            // Add the Merkle proof now that the transaction is confirmed in a block.
//                            existingTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
//                        }

//                        // Update the lookup cache with the new transaction information.
//                        // Since the WO record was not new it probably is already in the lookup cache.
//                        // Therefore, unconditionally update it.
//                        txs.AddOrUpdate(existingTransaction.Id, existingTransaction, (key, oldValue) => existingTransaction);

//                        transactions.AddOrUpdate(existingTransaction.Id, transaction, (key, oldValue) => transaction);
//                    }

//                    //this.SaveWatchOnlyWallet();
//                }
//            }

//            this.wallet.WatchedTransactions.TryGetValue(transactionHash.ToString(), out Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData existingWatchedTransaction);

//            if (existingWatchedTransaction != null && block != null)
//            {
//                // The transaction was previously stored, in an unconfirmed state.
//                // So now update the block hash and Merkle proof since it has
//                // appeared in a block.
//                existingWatchedTransaction.BlockHash = block.GetHash();
//                existingWatchedTransaction.MerkleProof = new MerkleBlock(block, new[] { transaction.GetHash() }).PartialMerkleTree;

//                // Update the lookup cache with the new transaction information.
//                txs.AddOrUpdate(existingWatchedTransaction.Id, existingWatchedTransaction, (key, oldValue) => existingWatchedTransaction);

//                transactions.AddOrUpdate(existingWatchedTransaction.Id, transaction, (key, oldValue) => transaction);

//                //this.SaveWatchOnlyWallet();
//            }
//        }

//        /// <summary>
//        /// Computes the value contained within the transactions currently
//        /// being watched for the given address by the watch-only wallet.
//        /// For high-precision applications use a block explorer instead,
//        /// as the returned balance only reflects fund movement since the
//        /// address first started being watched.
//        /// </summary>
//        /// <param name="address">The Base58 representation of the address to interrogate</param>
//        public Money GetRelativeBalance(string address)
//        {
//            Script scriptToCheck = BitcoinAddress.Create(address, this.network).ScriptPubKey;

//            var balance = new Money(0);

//            if (!this.wallet.WatchedAddresses.ContainsKey(scriptToCheck.ToString()))
//                // Returning zero would be misleading.
//                return null;

//            foreach (Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData transactionData in this.wallet.WatchedAddresses[scriptToCheck.ToString()].Transactions.Values)
//            {
//                var transaction = this.network.CreateTransaction(transactionData.Hex);

//                foreach (TxIn input in transaction.Inputs)
//                {
//                    // See if the previous transaction is in the watch-only wallet.
//                    this.txLookup.TryGetValue(input.PrevOut.Hash, out Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData prevTransactionData);

//                    // If it is null, it can't be related to the watched addresses (or it is the very first watched transaction)
//                    if (prevTransactionData == null)
//                        continue;

//                    var prevTransaction = this.network.CreateTransaction(prevTransactionData.Hex);

//                    // A sanity check to ensure the referenced output affects the desired address
//                    if (prevTransaction.Outputs[input.PrevOut.N].ScriptPubKey == scriptToCheck)
//                    {
//                        // Input = funds are being paid 'out of' the address in question

//                        // Computing the input value is not as straightforward as with an output, as the value is not directly
//                        // stored in a TxIn object. We need to check the previous output being spent by the input to get this
//                        // information. But even an OutPoint does not contain the Value - we need to check the other transactions
//                        // in the watch-only wallet to see if we have the prior transaction being referenced.

//                        // This does imply that the earliest transaction in the watch-only wallet (for this address) will not
//                        // have full previous transaction information stored. Therefore we can only reason about the address
//                        // balance after a given block height; any prior transactions are ignored.

//                        balance -= prevTransaction.Outputs[input.PrevOut.N].Value;
//                    }
//                }

//                // Check if the outputs contain the watched address
//                foreach (TxOut output in transaction.Outputs)
//                {
//                    if (output.ScriptPubKey == scriptToCheck)
//                    {
//                        // Output = funds are being paid 'into' the address in question
//                        balance += output.Value;
//                    }
//                }
//            }

//            return balance;
//        }

//        public void SyncFromDate(DateTime date)
//        {
//            this.logger.LogTrace("({0}:'{1}')", nameof(date), date);

//            // Before we start syncing we need to make sure that the chain is at a certain level.
//            // If the chain is behind the date from which we want to sync, we wait for it to catch up, and then we start syncing.
//            // If the chain is already past the date we want to sync from, we don't wait, even though the chain might not be fully downloaded.
//            if (this.chain.Tip.Header.BlockTime.LocalDateTime < date)
//            {
//                this.logger.LogTrace("The chain tip's date ({0}) is behind the date from which we want to sync ({1}). Waiting for the chain to catch up.", this.chain.Tip.Header.BlockTime.LocalDateTime, date);

//                this.asyncLoop = this.asyncLoopFactory.RunUntil("LightWalletSyncManager.SyncFromDate", this.nodeLifetime.ApplicationStopping,
//                    () => this.chain.Tip.Header.BlockTime.LocalDateTime >= date,
//                    () =>
//                    {
//                        this.logger.LogTrace("Start syncing from {0}.", date);
//                        this.StartSyncAsync(this.chain.GetHeightAtTime(date)).Wait();
//                    },
//                    (ex) =>
//                    {
//                        // in case of an exception while waiting for the chain to be at a certain height, we just cut our losses and
//                        // sync from the current height.
//                        this.logger.LogError("Exception occurred while waiting for chain to download: {0}.", ex.Message);
//                        this.StartSyncAsync(this.chain.Tip.Height).Wait();
//                    },
//                    TimeSpans.FiveSeconds);
//            }
//            else
//            {
//                // TODO: Handle exceptions and errors with the async call of sync.
//                Task.Run(() => this.StartSyncAsync(this.chain.GetHeightAtTime(date)));
//                this.logger.LogTrace("Start syncing from {0}", date);
//                //this.StartSync(this.chain.GetHeightAtTime(date));
//            }

//            this.logger.LogTrace("(-)");
//        }

//        /// <summary>
//        /// The last processed block.
//        /// </summary>
//        uint256 WalletTipHash { get; set; }

//        /// <summary>
//        /// Starts pulling blocks from the required height.
//        /// </summary>
//        /// <param name="height">The height from which to get blocks.</param>
//        private async Task StartSyncAsync(int height)
//        {
//            // TODO add support for the case where there is a reorg, like in the initialize method
//            ChainedHeader chainedHeader = this.chain.GetBlock(height);
//            this.walletTip = chainedHeader ?? throw new WalletException("Invalid block height");
//            this.WalletTipHash = chainedHeader.HashBlock;

//            this.logger.LogInformation("Start syncing (async) from  height: {0}.", height);

//            // Process the first block.
//            if (chainedHeader.BlockDataAvailability == BlockDataAvailabilityState.BlockAvailable)
//            {
//                this.ProcessBlock(chainedHeader.Block);
//            }
//            else
//            {
//                this.logger.LogInformation("Getting block: {0}.", chainedHeader.HashBlock);
//                Block initialBlock = await this.blockStoreCache.GetBlockAsync(chainedHeader.HashBlock);

//                this.logger.LogInformation("Process block: {0}.", chainedHeader.HashBlock);
//                this.ProcessBlock(initialBlock);
//            }

//            foreach (ChainedHeader block in this.chain.EnumerateAfter(chainedHeader))
//            {
//                if (block.BlockDataAvailability == BlockDataAvailabilityState.BlockAvailable)
//                {
//                    this.ProcessBlock(block.Block);
//                }
//                else
//                {
//                    this.logger.LogInformation("Getting block: {0}.", block.HashBlock);
//                    Block fullBlock = await this.blockStoreCache.GetBlockAsync(block.HashBlock);
//                    this.logger.LogInformation("Process block: {0}.", block.HashBlock);
//                    this.ProcessBlock(fullBlock);
//                }
//            }
//        }
//    }
//}
