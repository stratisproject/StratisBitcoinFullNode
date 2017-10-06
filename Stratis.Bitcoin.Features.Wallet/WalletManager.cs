﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Broadcasting;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.FileStorage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;
using Transaction = NBitcoin.Transaction;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Wallet.Tests")]
namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// A manager providing operations on wallets.
    /// </summary>
    public class WalletManager : IWalletManager
    {
        /// <summary>
        /// A lock object that protects access to the <see cref="Wallet"/>.
        /// Any of the collections inside Wallet must be synchronized using this lock.
        /// </summary>
        private readonly object lockObject;

        /// <summary>The async loop we need to wait upon before we can shut down this manager.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        public ConcurrentBag<Wallet> Wallets { get; }

        // Size of the buffer of unused addresses maintained in an account. 
        private const int UnusedAddressesBuffer = 20;

        // Quantity of accounts created in a wallet file when a wallet is restored.
        private const int WalletRecoveryAccountsCount = 1;

        // Quantity of accounts created in a wallet file when a wallet is created.
        private const int WalletCreationAccountsCount = 1;

        // File extension for wallet files.
        private const string WalletFileExtension = "wallet.json";

        // Timer for saving wallet files to the file system.
        private const int WalletSavetimeIntervalInMinutes = 5;

        private readonly CoinType coinType;
        private readonly Network network;
        private readonly IConnectionManager connectionManager;
        private readonly ConcurrentChain chain;
        private readonly NodeSettings settings;
        private readonly IWalletFeePolicy walletFeePolicy;
        private readonly IMempoolValidator mempoolValidator;
        private readonly INodeLifetime nodeLifetime;
        private readonly ILogger logger;
        private readonly FileStorage<Wallet> fileStorage;
        private readonly IBroadcasterManager broadcasterManager;

        public uint256 WalletTipHash { get; set; }

        //TODO: a second lookup dictionary is proposed to lookup for spent outputs
        // every time we find a trx that credits we need to add it to this lookup
        // private Dictionary<OutPoint, TransactionData> outpointLookup;

        internal Dictionary<Script, HdAddress> keysLookup;

        /// <summary>
        /// Occurs when a transaction is found.
        /// </summary>
        public event EventHandler<TransactionFoundEventArgs> TransactionFound;

        public WalletManager(
            ILoggerFactory loggerFactory,
            IConnectionManager connectionManager,
            Network network,
            ConcurrentChain chain,
            NodeSettings settings, DataFolder dataFolder,
            IWalletFeePolicy walletFeePolicy,
            IAsyncLoopFactory asyncLoopFactory,
            INodeLifetime nodeLifetime,
            IMempoolValidator mempoolValidator = null, // mempool does not exist in a light wallet
            IBroadcasterManager broadcasterManager = null) // no need to know about transactions the node broadcasted
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(dataFolder, nameof(dataFolder));
            Guard.NotNull(walletFeePolicy, nameof(walletFeePolicy));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));

            this.lockObject = new object();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Wallets = new ConcurrentBag<Wallet>();

            this.connectionManager = connectionManager;
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chain = chain;
            this.settings = settings;
            this.walletFeePolicy = walletFeePolicy;
            this.mempoolValidator = mempoolValidator;
            this.asyncLoopFactory = asyncLoopFactory;
            this.nodeLifetime = nodeLifetime;
            this.fileStorage = new FileStorage<Wallet>(dataFolder.WalletPath);
            this.broadcasterManager = broadcasterManager;

            // register events
            if (this.broadcasterManager != null)
            {
                this.broadcasterManager.TransactionStateChanged += this.BroadcasterManager_TransactionStateChanged;
            }
        }

        private void BroadcasterManager_TransactionStateChanged(object sender, TransactionBroadcastEntry transactionEntry)
        {
            if (transactionEntry.State == State.Propagated)
            {
                this.ProcessTransaction(transactionEntry.Transaction);
            }
        }

        public void Start()
        {
            // find wallets and load them in memory
            var wallets = this.fileStorage.LoadByFileExtension(WalletFileExtension);

            foreach (var wallet in wallets)
            {
                this.Wallets.Add(wallet);
            }

            // load data in memory for faster lookups
            this.LoadKeysLookupLock();

            // find the last chain block received by the wallet manager.
            this.WalletTipHash = this.LastReceivedBlockHash();

            // save the wallets file every 5 minutes to help against crashes.
            this.asyncLoop = this.asyncLoopFactory.Run("wallet persist job", token =>
            {
                this.SaveWallets();
                this.logger.LogInformation($"Wallets saved to file at {DateTime.Now}.");
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
            {
                this.broadcasterManager.TransactionStateChanged -= this.BroadcasterManager_TransactionStateChanged;
            }

            if (this.asyncLoop != null)
                this.asyncLoop.Dispose();

            this.SaveWallets();
        }

        /// <inheritdoc />
        public Mnemonic CreateWallet(string password, string name, string passphrase = null, string mnemonicList = null)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));

            // for now the passphrase is set to be the password by default.
            if (passphrase == null)
            {
                passphrase = password;
            }

            // generate the root seed used to generate keys from a mnemonic picked at random 
            // and a passphrase optionally provided by the user            
            Mnemonic mnemonic = string.IsNullOrEmpty(mnemonicList)
                ? new Mnemonic(Wordlist.English, WordCount.Twelve)
                : new Mnemonic(mnemonicList);
            ExtKey extendedKey = HdOperations.GetHdPrivateKey(mnemonic, passphrase);

            // create a wallet file 
            string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.network).ToWif();
            Wallet wallet = this.GenerateWalletFile(name, encryptedSeed, extendedKey.ChainCode);

            // generate multiple accounts and addresses from the get-go
            for (int i = 0; i < WalletCreationAccountsCount; i++)
            {
                HdAccount account = wallet.AddNewAccount(password, this.coinType);
                account.CreateAddresses(this.network, UnusedAddressesBuffer);
                account.CreateAddresses(this.network, UnusedAddressesBuffer, true);
            }

            // update the height of the we start syncing from
            this.UpdateLastBlockSyncedHeight(wallet, this.chain.Tip);

            // save the changes to the file and add addresses to be tracked
            this.SaveWallet(wallet);
            this.Load(wallet);
            this.LoadKeysLookupLock();

            return mnemonic;
        }

        /// <inheritdoc />
        public Wallet LoadWallet(string password, string name)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));

            // load the file from the local system
            Wallet wallet = this.fileStorage.LoadByFileName($"{name}.{WalletFileExtension}");

            // Check the password
            try
            {
                Key.Parse(wallet.EncryptedSeed, password, wallet.Network);
            }
            catch (Exception ex)
            {
                throw new SecurityException(ex.Message);
            }

            this.Load(wallet);
            return wallet;
        }

        /// <inheritdoc />
        public Wallet RecoverWallet(string password, string name, string mnemonic, DateTime creationTime, string passphrase = null)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));
            Guard.NotEmpty(mnemonic, nameof(mnemonic));

            // for now the passphrase is set to be the password by default.
            if (passphrase == null)
            {
                passphrase = password;
            }

            // generate the root seed used to generate keys
            ExtKey extendedKey;
            try
            {
                extendedKey = HdOperations.GetHdPrivateKey(mnemonic, passphrase);
            }
            catch (NotSupportedException ex)
            {
                if (ex.Message == "Unknown")
                {
                    throw new WalletException("Please make sure you enter valid mnemonic words.");
                }

                throw;
            }


            // create a wallet file 
            string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.network).ToWif();
            Wallet wallet = this.GenerateWalletFile(name, encryptedSeed, extendedKey.ChainCode, creationTime);

            // generate multiple accounts and addresses from the get-go
            for (int i = 0; i < WalletRecoveryAccountsCount; i++)
            {
                HdAccount account = wallet.AddNewAccount(password, this.coinType);
                account.CreateAddresses(this.network, UnusedAddressesBuffer);
                account.CreateAddresses(this.network, UnusedAddressesBuffer, true);
            }

            int blockSyncStart = this.chain.GetHeightAtTime(creationTime);
            this.UpdateLastBlockSyncedHeight(wallet, this.chain.GetBlock(blockSyncStart));

            // save the changes to the file and add addresses to be tracked
            this.SaveWallet(wallet);
            this.Load(wallet);
            this.LoadKeysLookupLock();

            return wallet;
        }

        /// <inheritdoc />
        public HdAccount GetUnusedAccount(string walletName, string password)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotEmpty(password, nameof(password));

            Wallet wallet = this.GetWalletByName(walletName);

            return this.GetUnusedAccount(wallet, password);
        }

        /// <inheritdoc />
        public HdAccount GetUnusedAccount(Wallet wallet, string password)
        {
            Guard.NotNull(wallet, nameof(wallet));
            Guard.NotEmpty(password, nameof(password));

            HdAccount account;

            lock (this.lockObject)
            {
                account = wallet.GetFirstUnusedAccount(this.coinType);

                if (account != null)
                {
                    return account;
                }

                // No unused account was found, create a new one.
                account = wallet.AddNewAccount(password, this.coinType);
            }

            // save the changes to the file
            this.SaveWallet(wallet);

            return account;
        }

        public string GetExtPubKey(WalletAccountReference accountReference)
        {
            Guard.NotNull(accountReference, nameof(accountReference));

            Wallet wallet = this.GetWalletByName(accountReference.WalletName);

            lock (this.lockObject)
            {
                // get the account
                HdAccount account = wallet.GetAccountByCoinType(accountReference.AccountName, this.coinType);

                return account.ExtendedPubKey;
            }
        }

        /// <inheritdoc />
        public HdAddress GetUnusedAddress(WalletAccountReference accountReference)
        {
            return this.GetUnusedAddresses(accountReference, 1).Single();
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count)
        {
            Guard.NotNull(accountReference, nameof(accountReference));
            Guard.Assert(count > 0);

            Wallet wallet = this.GetWalletByName(accountReference.WalletName);

            bool generated = false;
            IEnumerable<HdAddress> addresses;

            lock (this.lockObject)
            {
                // get the account
                HdAccount account = wallet.GetAccountByCoinType(accountReference.AccountName, this.coinType);

                var unusedAddresses = account.ExternalAddresses.Where(acc => !acc.Transactions.Any()).ToList();
                var diff = unusedAddresses.Count - count;
                if (diff < 0)
                {
                    account.CreateAddresses(this.network, Math.Abs(diff), isChange: false);

                    generated = true;
                }

                addresses = account
                    .ExternalAddresses
                    .Where(acc => !acc.Transactions.Any())
                    .OrderBy(x => x.Index)
                    .Take(count);
            }

            if (generated)
            {
                // adds the address to the list of tracked addresses
                this.LoadKeysLookupLock();

                // save the changes to the file
                this.SaveWallet(wallet);
            }

            return addresses;
        }

        /// <inheritdoc />
        public HdAddress GetOrCreateChangeAddress(HdAccount account)
        {
            HdAddress changeAddress = null;

            lock (this.lockObject)
            {
                // get address to send the change to
                changeAddress = account.GetFirstUnusedChangeAddress();

                // no more change addresses left. create a new one.
                if (changeAddress == null)
                {
                    var accountAddress = account.CreateAddresses(this.network, 1, isChange: true).Single();
                    changeAddress = account.InternalAddresses.First(a => a.Address == accountAddress);
                }
            }

            if (changeAddress == null)
            {
                // adds the address to the list of tracked addresses
                this.LoadKeysLookupLock();
            }

            return changeAddress;
        }

        /// <inheritdoc />
        public (string folderPath, IEnumerable<string>) GetWalletsFiles()
        {
            return (this.fileStorage.FolderPath, this.fileStorage.GetFilesNames(this.GetWalletFileExtension()));
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetHistory(string walletName)
        {
            Guard.NotEmpty(walletName, nameof(walletName));

            Wallet wallet = this.GetWalletByName(walletName);

            return this.GetHistory(wallet);
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetHistory(Wallet wallet)
        {
            lock (this.lockObject)
            {
                return this.GetHistoryInternal(wallet).ToList();
            }
        }

        private IEnumerable<HdAddress> GetHistoryInternal(Wallet wallet)
        {
            var accounts = wallet.GetAccountsByCoinType(this.coinType).ToList();
            if (accounts.Count == 0)
            {
                yield break;
            }

            foreach (var address in accounts.SelectMany(a => a.ExternalAddresses).Concat(accounts.SelectMany(a => a.InternalAddresses)))
            {
                if (address.Transactions.Any())
                {
                    yield return address;
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<FlatHistory> GetFlatHistory(string walletName)
        {
            // in order to calculate the fee properly we need to retrieve all the transactions with spending details.
            var wallet = this.GetWalletByName(walletName);

            lock (this.lockObject)
            {
                // get transactions contained in the wallet
                var items = this.GetHistoryInternal(wallet).SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToList();

                return items;
            }
        }

        /// <inheritdoc />
        public Wallet GetWallet(string walletName)
        {
            Guard.NotEmpty(walletName, nameof(walletName));

            Wallet wallet = this.GetWalletByName(walletName);
            return wallet;
        }

        /// <inheritdoc />
        public IEnumerable<HdAccount> GetAccounts(string walletName)
        {
            Guard.NotEmpty(walletName, nameof(walletName));

            Wallet wallet = this.GetWalletByName(walletName);

            lock (this.lockObject)
            {
                return wallet.GetAccountsByCoinType(this.coinType);
            }
        }

        public int LastBlockHeight()
        {
            if (!this.Wallets.Any())
            {
                return this.chain.Tip.Height;
            }

            lock (this.lockObject)
            {
                return this.Wallets.Min(w => w.AccountsRoot.SingleOrDefault(a => a.CoinType == this.coinType)?.LastBlockSyncedHeight) ?? 0;
            }
        }

        /// <inheritdoc />
        public bool ContainsWallets { get { return this.Wallets.Any(); } }

        /// <summary>
        /// Gets the hash of the oldest block received by the wallets.
        /// </summary>
        /// <returns></returns>
        public uint256 LastReceivedBlockHash()
        {
            if (!this.Wallets.Any())
            {
                return this.chain.Tip.HashBlock;
            }

            lock (this.lockObject)
            {
                var lastBlockSyncedHash = this.Wallets
                    .Select(w => w.AccountsRoot.SingleOrDefault(a => a.CoinType == this.coinType))
                    .Where(w => w != null)
                    .OrderBy(o => o.LastBlockSyncedHeight)
                    .FirstOrDefault()?.LastBlockSyncedHash;
                Guard.Assert(lastBlockSyncedHash != null);
                return lastBlockSyncedHash;
            }
        }

        /// <inheritdoc />
        public List<UnspentOutputReference> GetSpendableTransactionsInWallet(string walletName, int confirmations = 0)
        {
            Guard.NotEmpty(walletName, nameof(walletName));


            Wallet wallet = this.GetWalletByName(walletName);

            lock (this.lockObject)
            {
                return wallet.GetAllSpendableTransactions(this.coinType, this.chain.Tip.Height, confirmations);
            }
        }

        /// <inheritdoc />
        public List<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference walletAccountReference, int confirmations = 0)
        {
            Guard.NotNull(walletAccountReference, nameof(walletAccountReference));

            Wallet wallet = this.GetWalletByName(walletAccountReference.WalletName);

            lock (this.lockObject)
            {
                HdAccount account = wallet.GetAccountByCoinType(walletAccountReference.AccountName, this.coinType);

                if (account == null)
                {
                    throw new WalletException(
                        $"Account '{walletAccountReference.AccountName}' in wallet '{walletAccountReference.WalletName}' not found.");
                }

                return account.GetSpendableTransactions(this.chain.Tip.Height, confirmations);
            }
        }

        /// <inheritdoc />
        public void RemoveBlocks(ChainedBlock fork)
        {
            Guard.NotNull(fork, nameof(fork));

            if (this.keysLookup == null)
            {
                this.LoadKeysLookupLock();
            }

            lock (this.lockObject)
            {
                var allAddresses = this.keysLookup.Values;
                foreach (var address in allAddresses)
                {
                    var toRemove = address.Transactions.Where(w => w.BlockHeight > fork.Height).ToList();
                    foreach (var transactionData in toRemove)
                        address.Transactions.Remove(transactionData);
                }

                this.UpdateLastBlockSyncedHeight(fork);
            }
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block, ChainedBlock chainedBlock)
        {
            Guard.NotNull(block, nameof(block));
            Guard.NotNull(chainedBlock, nameof(chainedBlock));

            this.logger.LogTrace($"block notification - height: {chainedBlock.Height}, hash: {block.Header.GetHash()}, coin: {this.coinType}");

            // if there is no wallet yet, update the wallet tip hash and do nothing else.
            if (!this.Wallets.Any())
            {
                this.WalletTipHash = chainedBlock.HashBlock;
                return;
            }

            // is this the next block
            if (chainedBlock.Header.HashPrevBlock != this.WalletTipHash)
            {
                // are we still on the main chain
                var current = this.chain.GetBlock(this.WalletTipHash);
                if (current == null)
                    throw new WalletException("Reorg");

                // the block coming in to the wallet should
                // never be ahead of the wallet, if the block is behind let it pass
                if (chainedBlock.Height > current.Height)
                    throw new WalletException("block too far in the future has arrived to the wallet");
            }

            lock (this.lockObject)
            {
                foreach (Transaction transaction in block.Transactions)
                {
                    this.ProcessTransaction(transaction, chainedBlock.Height, block);
                }

                // update the wallets with the last processed block height
                this.UpdateLastBlockSyncedHeight(chainedBlock);
            }
        }

        /// <inheritdoc />
        public void ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null)
        {
            Guard.NotNull(transaction, nameof(transaction));

            var hash = transaction.GetHash();
            this.logger.LogTrace($"transaction received - hash: {hash}, coin: {this.coinType}");

            // load the keys for lookup if they are not loaded yet.
            if (this.keysLookup == null)
            {
                this.LoadKeysLookupLock();
            }

            var foundtrx = new List<Tuple<Script, uint256>>();

            lock (this.lockObject)
            {
                // check the outputs
                foreach (TxOut utxo in transaction.Outputs)
                {
                    // check if the outputs contain one of our addresses
                    if (this.keysLookup.TryGetValue(utxo.ScriptPubKey, out HdAddress pubKey))
                    {
                        this.AddTransactionToWallet(transaction.ToHex(), hash, transaction.Time, transaction.Outputs.IndexOf(utxo), utxo.Value, utxo.ScriptPubKey, blockHeight, block);
                        foundtrx.Add(Tuple.Create(utxo.ScriptPubKey, hash));
                    }
                }

                // check the inputs - include those that have a reference to a transaction containing one of our scripts and the same index            
                foreach (TxIn input in transaction.Inputs.Where(txIn => this.keysLookup.Values.Distinct().SelectMany(v => v.Transactions).Any(trackedTx => trackedTx.Id == txIn.PrevOut.Hash && trackedTx.Index == txIn.PrevOut.N)))
                {
                    TransactionData tTx = this.keysLookup.Values.Distinct().SelectMany(v => v.Transactions).Single(trackedTx => trackedTx.Id == input.PrevOut.Hash && trackedTx.Index == input.PrevOut.N);

                    // find the script this input references
                    var keyToSpend = this.keysLookup.First(v => v.Value.Transactions.Contains(tTx)).Key;

                    // get the details of the outputs paid out. 
                    IEnumerable<TxOut> paidoutto = transaction.Outputs.Where(o =>
                    {
                        // if script is empty ignore it
                        if (o.IsEmpty)
                            return false;

                        var found = this.keysLookup.TryGetValue(o.ScriptPubKey, out HdAddress addr);

                        // include the keys we don't hold
                        if (!found)
                            return true;

                        // include the keys we do hold but that are for receiving 
                        // addresses (which would mean the user paid itself).
                        return !addr.IsChangeAddress();
                    });

                    this.AddSpendingTransactionToWallet(transaction.ToHex(), hash, transaction.Time, paidoutto, tTx.Id, tTx.Index, blockHeight, block);
                }
            }

            if (foundtrx.Any())
            {
                this.LoadKeysLookupLock();

                foreach (var tuple in foundtrx)
                {
                    // notify a transaction has been found
                    this.TransactionFound?.Invoke(this, new TransactionFoundEventArgs(tuple.Item1, tuple.Item2));
                }
            }
        }

        /// <summary>
        /// Adds the transaction to the wallet.
        /// </summary>
        /// <param name="transactionHash">The transaction hash.</param>
        /// <param name="time">The time.</param>
        /// <param name="index">The index.</param>
        /// <param name="amount">The amount.</param>
        /// <param name="script">The script.</param>
        /// <param name="blockHeight">Height of the block.</param>
        /// <param name="block">The block containing the transaction to add.</param>
        /// <param name="transactionHex">The hexadecimal representation of the transaction.</param>
        private void AddTransactionToWallet(string transactionHex, uint256 transactionHash, uint time, int index, Money amount, Script script,
            int? blockHeight = null, Block block = null)
        {
            // get the collection of transactions to add to.
            this.keysLookup.TryGetValue(script, out HdAddress address);
            var addressTransactions = address.Transactions;

            // check if a similar UTXO exists or not (same transaction id and same index)
            // new UTXOs are added, existing ones are updated
            var foundTransaction = addressTransactions.FirstOrDefault(t => t.Id == transactionHash && t.Index == index);
            if (foundTransaction == null)
            {
                var newTransaction = new TransactionData
                {
                    Amount = amount,
                    BlockHeight = blockHeight,
                    BlockHash = block?.GetHash(),
                    Id = transactionHash,
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? time),
                    Index = index,
                    ScriptPubKey = script,
                    Hex = transactionHex
                };

                // add the Merkle proof to the (non-spending) transaction
                if (block != null)
                {
                    newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                }

                addressTransactions.Add(newTransaction);
            }
            else
            {
                // update the block height and block hash
                if (foundTransaction.BlockHeight == null && blockHeight != null)
                {
                    foundTransaction.BlockHeight = blockHeight;
                    foundTransaction.BlockHash = block?.GetHash();
                }

                // update the block time
                if (block != null)
                {
                    foundTransaction.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
                }

                // add the Merkle proof now that the transaction is confirmed in a block
                if (block != null && foundTransaction.MerkleProof == null)
                {
                    foundTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                }
            }

            this.TransactionFoundInternal(script);
        }

        /// <summary>
        /// Adds the transaction to the wallet.
        /// </summary>
        /// <param name="transactionHash">The transaction hash.</param>
        /// <param name="time">The time.</param>
        /// <param name="paidToOutputs">A list of payments made out</param>
        /// <param name="spendingTransactionId">The id of the transaction containing the output being spent, if this is a spending transaction.</param>
        /// <param name="spendingTransactionIndex">The index of the output in the transaction being referenced, if this is a spending transaction.</param>
        /// <param name="blockHeight">Height of the block.</param>
        /// <param name="block">The block containing the transaction to add.</param>
        /// <param name="transactionHex">The hexadecimal representation of the transaction.</param>
        private void AddSpendingTransactionToWallet(string transactionHex, uint256 transactionHash, uint time, IEnumerable<TxOut> paidToOutputs,
            uint256 spendingTransactionId, int? spendingTransactionIndex, int? blockHeight = null, Block block = null)
        {
            // get the transaction being spent
            TransactionData spentTransaction = this.keysLookup.Values.Distinct().SelectMany(v => v.Transactions)
                .SingleOrDefault(t => t.Id == spendingTransactionId && t.Index == spendingTransactionIndex);
            if (spentTransaction == null)
            {
                // strange, why would it be null?
                return;
            }

            // if the details of this spending transaction are seen for the first time
            if (spentTransaction.SpendingDetails == null)
            {
                List<PaymentDetails> payments = new List<PaymentDetails>();
                foreach (var paidToOutput in paidToOutputs)
                {
                    payments.Add(new PaymentDetails
                    {
                        DestinationScriptPubKey = paidToOutput.ScriptPubKey,
                        DestinationAddress = paidToOutput.ScriptPubKey.GetDestinationAddress(this.network)?.ToString(),
                        Amount = paidToOutput.Value
                    });
                }

                SpendingDetails spendingDetails = new SpendingDetails
                {
                    TransactionId = transactionHash,
                    Payments = payments,
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? time),
                    BlockHeight = blockHeight,
                    Hex = transactionHex
                };

                spentTransaction.SpendingDetails = spendingDetails;
                spentTransaction.MerkleProof = null;
            }
            else // if this spending transaction is being confirmed in a block
            {
                // update the block height
                if (spentTransaction.SpendingDetails.BlockHeight == null && blockHeight != null)
                {
                    spentTransaction.SpendingDetails.BlockHeight = blockHeight;
                }

                // update the block time to be that of the block in which the transaction is confirmed
                if (block != null)
                {
                    spentTransaction.SpendingDetails.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
                }
            }
        }

        private void TransactionFoundInternal(Script script)
        {
            foreach (Wallet wallet in this.Wallets)
            {
                foreach (var account in wallet.GetAccountsByCoinType(this.coinType))
                {
                    bool isChange;
                    if (account.ExternalAddresses.Any(address => address.ScriptPubKey == script))
                    {
                        isChange = false;
                    }
                    else if (account.InternalAddresses.Any(address => address.ScriptPubKey == script))
                    {
                        isChange = true;
                    }
                    else
                    {
                        continue;
                    }

                    // calculate how many accounts to add to keep a buffer of 20 unused addresses
                    int lastUsedAddressIndex = account.GetLastUsedAddress(isChange).Index;
                    int addressesCount = isChange ? account.InternalAddresses.Count() : account.ExternalAddresses.Count();
                    int emptyAddressesCount = addressesCount - lastUsedAddressIndex - 1;
                    int accountsToAdd = UnusedAddressesBuffer - emptyAddressesCount;
                    account.CreateAddresses(this.network, accountsToAdd, isChange);
                }
            }
        }

        /// <inheritdoc />
        public void DeleteWallet()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void SaveWallets()
        {
            foreach (var wallet in this.Wallets)
            {
                this.SaveWallet(wallet);
            }
        }

        /// <inheritdoc />
        public void SaveWallet(Wallet wallet)
        {
            Guard.NotNull(wallet, nameof(wallet));

            lock (this.lockObject)
            {
                this.fileStorage.SaveToFile(wallet, $"{wallet.Name}.{WalletFileExtension}");
            }
        }

        /// <inheritdoc />
        public string GetWalletFileExtension()
        {
            return WalletFileExtension;
        }

        /// <inheritdoc />
        public void UpdateLastBlockSyncedHeight(ChainedBlock chainedBlock)
        {
            Guard.NotNull(chainedBlock, nameof(chainedBlock));

            // update the wallets with the last processed block height                        
            foreach (var wallet in this.Wallets)
            {
                this.UpdateLastBlockSyncedHeight(wallet, chainedBlock);
            }

            this.WalletTipHash = chainedBlock.HashBlock;
        }

        /// <inheritdoc />
        public void UpdateLastBlockSyncedHeight(Wallet wallet, ChainedBlock chainedBlock)
        {
            Guard.NotNull(wallet, nameof(wallet));
            Guard.NotNull(chainedBlock, nameof(chainedBlock));

            // the block locator will help when the wallet 
            // needs to rewind this will be used to find the fork 
            wallet.BlockLocator = chainedBlock.GetLocator().Blocks;

            lock (this.lockObject)
            {
                // update the wallets with the last processed block height
                foreach (var accountRoot in wallet.AccountsRoot.Where(a => a.CoinType == this.coinType))
                {
                    accountRoot.LastBlockSyncedHeight = chainedBlock.Height;
                    accountRoot.LastBlockSyncedHash = chainedBlock.HashBlock;
                }
            }
        }

        /// <summary>
        /// Generates the wallet file.
        /// </summary>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="encryptedSeed">The seed for this wallet, password encrypted.</param>
        /// <param name="chainCode">The chain code.</param>
        /// <param name="creationTime">The time this wallet was created.</param>
        /// <returns>The wallet object that was saved into the file system.</returns>
        /// <exception cref="System.NotSupportedException"></exception>
        private Wallet GenerateWalletFile(string name, string encryptedSeed, byte[] chainCode, DateTimeOffset? creationTime = null)
        {
            Guard.NotEmpty(name, nameof(name));
            Guard.NotEmpty(encryptedSeed, nameof(encryptedSeed));
            Guard.NotNull(chainCode, nameof(chainCode));

            if (this.fileStorage.Exists($"{name}.{WalletFileExtension}"))
            {
                throw new WalletException($"Wallet with name '{name}' already exists.");
            }

            List<Wallet> similarWallets = this.Wallets.Where(w => w.EncryptedSeed == encryptedSeed).ToList();
            if (similarWallets.Any())
            {
                throw new WalletException($"Cannot create this wallet as a wallet with the same private key already exists. If you want to restore your wallet from scratch, " +
                                                    $"please remove the file {string.Join(", ", similarWallets.Select(w => w.Name))}.{WalletFileExtension} from '{this.fileStorage.FolderPath}' and try restoring the wallet again. " +
                                                    $"Make sure you have your mnemonic and your password handy!");
            }

            Wallet walletFile = new Wallet
            {
                Name = name,
                EncryptedSeed = encryptedSeed,
                ChainCode = chainCode,
                CreationTime = creationTime ?? DateTimeOffset.Now,
                Network = this.network,
                AccountsRoot = new List<AccountRoot> { new AccountRoot { Accounts = new List<HdAccount>(), CoinType = this.coinType } },
            };

            // create a folder if none exists and persist the file
            this.fileStorage.SaveToFile(walletFile, $"{name}.{WalletFileExtension}");
            return walletFile;
        }

        /// <summary>
        /// Loads the wallet to be used by the manager.
        /// </summary>
        /// <param name="wallet">The wallet to load.</param>
        private void Load(Wallet wallet)
        {
            Guard.NotNull(wallet, nameof(wallet));

            if (this.Wallets.Any(w => w.Name == wallet.Name))
            {
                return;
            }

            this.Wallets.Add(wallet);
        }

        /// <summary>
        /// Loads the keys and transactions we're tracking in memory for faster lookups.
        /// </summary>
        /// <returns></returns>
        public void LoadKeysLookupLock()
        {
            lock (this.lockObject)
            {
                var lookup = new Dictionary<Script, HdAddress>();
                foreach (var wallet in this.Wallets)
                {
                    var accounts = wallet.GetAccountsByCoinType(this.coinType);
                    foreach (var account in accounts)
                    {
                        var addresses = account.ExternalAddresses.Concat(account.InternalAddresses);
                        foreach (var address in addresses)
                        {
                            lookup.Add(address.ScriptPubKey, address);
                            if (address.Pubkey != null)
                                lookup.Add(address.Pubkey, address);
                        }
                    }
                }

                this.keysLookup = lookup;
            }
        }

        /// <inheritdoc />
        public string[] GetWalletsNames()
        {
            return this.Wallets.Select(w => w.Name).ToArray();
        }

        /// <inheritdoc />
        public Wallet GetWalletByName(string walletName)
        {
            Wallet wallet = this.Wallets.SingleOrDefault(w => w.Name == walletName);
            if (wallet == null)
            {
                throw new WalletException($"No wallet with name {walletName} could be found.");
            }

            return wallet;
        }
    }

    public class TransactionFoundEventArgs : EventArgs
    {
        public Script Script { get; set; }

        public uint256 TransactionHash { get; set; }

        public TransactionFoundEventArgs(Script script, uint256 transactionHash)
        {
            this.Script = script;
            this.TransactionHash = transactionHash;
        }
    }
}