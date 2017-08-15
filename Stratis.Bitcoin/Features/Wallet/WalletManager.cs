using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Utilities;
using Transaction = NBitcoin.Transaction;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// A manager providing operations on wallets.
    /// </summary>
    public class WalletManager : IWalletManager
    {
        public ConcurrentBag<Wallet> Wallets { get; }

        private const int UnusedAddressesBuffer = 20;
        private const int WalletRecoveryAccountsCount = 3;
        private const int WalletCreationAccountsCount = 2;
        private const string WalletFileExtension = "wallet.json";
        private const int WalletSavetimeIntervalInMinutes = 5;

        private readonly CoinType coinType;
        private readonly Network network;
        private readonly IConnectionManager connectionManager;
        private readonly ConcurrentChain chain;
        private readonly NodeSettings settings;
        private readonly DataFolder dataFolder;
        private readonly IWalletFeePolicy walletFeePolicy;
        private readonly IMempoolValidator mempoolValidator;
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly INodeLifetime nodeLifetime;
        private readonly ILogger logger;

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
            IMempoolValidator mempoolValidator = null) // mempool does not exist in a light wallet
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(dataFolder, nameof(dataFolder));
            Guard.NotNull(walletFeePolicy, nameof(walletFeePolicy));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));


            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Wallets = new ConcurrentBag<Wallet>();

            this.connectionManager = connectionManager;
            this.network = network;
            this.coinType = (CoinType) network.Consensus.CoinType;
            this.chain = chain;
            this.settings = settings;
            this.dataFolder = dataFolder;
            this.walletFeePolicy = walletFeePolicy;
            this.mempoolValidator = mempoolValidator;
            this.asyncLoopFactory = asyncLoopFactory;
            this.nodeLifetime = nodeLifetime;

            // register events
            this.TransactionFound += this.OnTransactionFound;
        }

        public void Initialize()
        {
            // find wallets and load them in memory
            foreach (var path in this.GetWalletFilesPaths())
            {
                this.Load(this.DeserializeWallet(path));
            }

            // load data in memory for faster lookups
            this.LoadKeysLookup();

            // find the last chain block received by the wallet manager.
            this.WalletTipHash = this.LastReceivedBlockHash();

            // save the wallets file every 5 minutes to help against crashes.
            this.asyncLoopFactory.Run("wallet persist job", token =>
                {
                    this.SaveToFile();
                    this.logger.LogInformation($"Wallets saved to file at {DateTime.Now}.");
                    return Task.CompletedTask;
                },
                this.nodeLifetime.ApplicationStopping,
                repeatEvery: TimeSpan.FromMinutes(WalletSavetimeIntervalInMinutes),
                startAfter: TimeSpan.FromMinutes(WalletSavetimeIntervalInMinutes));
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
            Wallet wallet = this.GenerateWalletFile(password, name, extendedKey);

            // generate multiple accounts and addresses from the get-go
            for (int i = 0; i < WalletCreationAccountsCount; i++)
            {
                HdAccount account = wallet.AddNewAccount(password, this.coinType);
                this.CreateAddressesInAccount(account, UnusedAddressesBuffer);
                this.CreateAddressesInAccount(account, UnusedAddressesBuffer, true);
            }

            // update the height of the we start syncing from
            this.UpdateLastBlockSyncedHeight(wallet, this.chain.Tip);

            // save the changes to the file and add addresses to be tracked
            this.SaveToFile(wallet);
            this.Load(wallet);
            this.LoadKeysLookup();

            return mnemonic;
        }

        /// <inheritdoc />
        public Wallet LoadWallet(string password, string name)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));

            var walletFilePath = Path.Combine(this.dataFolder.WalletPath, $"{name}.{WalletFileExtension}");

            // load the file from the local system
            Wallet wallet = this.DeserializeWallet(walletFilePath);

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
            ExtKey extendedKey = HdOperations.GetHdPrivateKey(mnemonic, passphrase);

            // create a wallet file 
            Wallet wallet = this.GenerateWalletFile(password, name, extendedKey, creationTime);

            // generate multiple accounts and addresses from the get-go
            for (int i = 0; i < WalletRecoveryAccountsCount; i++)
            {
                HdAccount account = wallet.AddNewAccount(password, this.coinType);
                this.CreateAddressesInAccount(account, UnusedAddressesBuffer);
                this.CreateAddressesInAccount(account, UnusedAddressesBuffer, true);
            }

            int blockSyncStart = this.chain.GetHeightAtTime(creationTime);
            this.UpdateLastBlockSyncedHeight(wallet, this.chain.GetBlock(blockSyncStart));

            // save the changes to the file and add addresses to be tracked
            this.SaveToFile(wallet);
            this.Load(wallet);
            this.LoadKeysLookup();

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

            // get the accounts root for this type of coin
            var accountsRoot = wallet.AccountsRoot.Single(a => a.CoinType == this.coinType);

            // check if an unused account exists
            if (accountsRoot.Accounts.Any())
            {
                // gets an unused account
                var firstUnusedAccount = accountsRoot.GetFirstUnusedAccount();
                if (firstUnusedAccount != null)
                {
                    return firstUnusedAccount;
                }
            }

            // all accounts contain transactions, create a new one
            var newAccount = wallet.AddNewAccount(password, this.coinType);

            // save the changes to the file
            this.SaveToFile(wallet);
            return newAccount;
        }
        
        /// <inheritdoc />
        public HdAddress GetUnusedAddress(WalletAccountReference accountReference)
        {
            Guard.NotNull(accountReference, nameof(accountReference));

            Wallet wallet = this.GetWalletByName(accountReference.WalletName);

            // get the account
            HdAccount account = this.GetAccounts(wallet).GetAccountByName(accountReference.AccountName);

            // validate address creation
            if (account.ExternalAddresses.Any())
            {
                // check last created address contains transactions.
                var firstUnusedExternalAddress = account.GetFirstUnusedReceivingAddress();
                if (firstUnusedExternalAddress != null)
                {
                    return firstUnusedExternalAddress;
                }
            }

            // creates an address
            this.CreateAddressesInAccount(account, 1);

            // persists the address to the wallet file
            this.SaveToFile(wallet);

            // adds the address to the list of tracked addresses
            this.LoadKeysLookup();
            return account.GetFirstUnusedReceivingAddress();
        }

        /// <inheritdoc />
        public HdAddress GetOrCreateChangeAddress(HdAccount account)
        {
            // get address to send the change to
            var changeAddress = account.GetFirstUnusedChangeAddress();

            // no more change addresses left. create a new one.
            if (changeAddress == null)
            {
                var accountAddress = this.CreateAddressesInAccount(account, 1, isChange: true).Single();
                changeAddress = account.InternalAddresses.First(a => a.Address == accountAddress);

                // persists the address to the wallet file
                this.SaveToFile();

                // adds the address to the list of tracked addresses
                this.LoadKeysLookup();
            }

            return changeAddress;
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

        /// <summary>
        /// Creates a number of addresses in the provided account.
        /// </summary>
        /// <param name="account">The account.</param>
        /// <param name="addressesQuantity">The number of addresses to create.</param>
        /// <param name="isChange">Whether the addresses added are change (internal) addresses or receiving (external) addresses.</param>
        /// <returns>A list of addresses in Base58.</returns>
        private List<string> CreateAddressesInAccount(HdAccount account, int addressesQuantity, bool isChange = false)
        {
            List<string> addressesCreated = new List<string>();

            var addresses = isChange ? account.InternalAddresses : account.ExternalAddresses;

            // gets the index of the last address with transactions
            int firstNewAddressIndex = 0;
            if (addresses.Any())
            {
                firstNewAddressIndex = addresses.Max(add => add.Index) + 1;
            }

            for (int i = firstNewAddressIndex; i < firstNewAddressIndex + addressesQuantity; i++)
            {
                // generate new receiving address
                PubKey pubkey = HdOperations.GeneratePublicKey(account.ExtendedPubKey, i, isChange);
                BitcoinPubKeyAddress address = pubkey.GetAddress(this.network);

                // add address details
                addresses.Add(new HdAddress
                {
                    Index = i,
                    HdPath = HdOperations.CreateHdPath((int)account.GetCoinType(), account.Index, i, isChange),
                    ScriptPubKey = address.ScriptPubKey,
                    Pubkey = pubkey.ScriptPubKey,
                    Address = address.ToString(),
                    Transactions = new List<TransactionData>()
                });

                addressesCreated.Add(address.ToString());
            }

            if (isChange)
            {
                account.InternalAddresses = addresses;
            }
            else
            {
                account.ExternalAddresses = addresses;
            }

            return addressesCreated;
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

            return wallet.GetAccountsByCoinType(this.coinType);
        }

        public int LastBlockHeight()
        {
            if (!this.Wallets.Any())
            {
                return this.chain.Tip.Height;
            }

            return this.Wallets.Min(w => w.AccountsRoot.SingleOrDefault(a => a.CoinType == this.coinType)?.LastBlockSyncedHeight) ?? 0;
        }

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

            var lastBlockSyncedHash = this.Wallets
                .Select(w => w.AccountsRoot.SingleOrDefault(a => a.CoinType == this.coinType))
                .Where(w => w != null)
                .OrderBy(o => o.LastBlockSyncedHeight)
                .FirstOrDefault()?.LastBlockSyncedHash;
            Guard.Assert(lastBlockSyncedHash != null);
            return lastBlockSyncedHash;
        }

        /// <inheritdoc />
        public List<UnspentAccountReference> GetSpendableTransactions(string walletName, int confirmations = 0)
        {
            Guard.NotEmpty(walletName, nameof(walletName));

            var accounts = this.GetAccounts(walletName);

            var walletAccounts = new List<UnspentAccountReference>();
            foreach (var account in accounts)
            {
                var walletAccount = new WalletAccountReference {AccountName = account.Name, WalletName = walletName};
                walletAccounts.Add(this.GetSpendableTransactions(walletAccount, confirmations));
            }

            return walletAccounts;
        }

        /// <inheritdoc />
        public UnspentAccountReference GetSpendableTransactions(WalletAccountReference walletAccountReference, int confirmations = 0)
        {
            Guard.NotNull(walletAccountReference, nameof(walletAccountReference));

            var accounts = this.GetAccounts(walletAccountReference.WalletName);
            var account = accounts.FirstOrDefault(n => n.Name == walletAccountReference.AccountName);

            if (account == null)
            {
                throw new WalletException($"Account '{walletAccountReference.AccountName}' in wallet '{walletAccountReference.WalletName}' not found.");
            }

            return this.GetSpendableTransactions(account, confirmations);
        }

        /// <inheritdoc />
        public UnspentAccountReference GetSpendableTransactions(HdAccount account, int confirmations = 0)
        {
            Guard.NotNull(account, nameof(account));

            // this will take all the spendable coins that belong to an account
            // and keep the reference to the HDAddress and HDAccount, this is useful
            // so later the private key can be calculated just from a given UTXO

            var currentHeight = this.chain.Tip.Height;

            var accountReference = new UnspentAccountReference();
            foreach (var address in account.GetCombinedAddresses())
            {
                var unspentTransactions = address.UnspentTransactions()
                    .Where(a => currentHeight - (a.BlockHeight ?? currentHeight) >= confirmations).ToList();

                foreach (var transactionData in unspentTransactions)
                {
                    accountReference.UnspentOutputs.Add(new UnspentOutputReference
                    {
                        Account = account,
                        Address = address,
                        Transaction = transactionData
                    });
                }
            }

            return accountReference;
        }

        /// <inheritdoc />        
        public ISecret GetKeyForAddress(string walletName, string password, HdAddress address)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotEmpty(password, nameof(password));
            Guard.NotNull(address, nameof(address));

            var wallet = this.GetWalletByName(walletName);

            // check if the wallet contains the address.
            if (!wallet.AccountsRoot.Any(r => r.Accounts.Any(
                a => a.ExternalAddresses.Any(i => i.Address == address.Address) ||
                     a.InternalAddresses.Any(i => i.Address == address.Address))))
            {
                throw new WalletException("Address not found on wallet.");
            }

            // get extended private key
            Key privateKey = HdOperations.DecryptSeed(wallet.EncryptedSeed, password, wallet.Network);
            return HdOperations.GetExtendedPrivateKey(privateKey, wallet.ChainCode, address.HdPath, wallet.Network);
        }
        
        /// <inheritdoc />
        public bool SendTransaction(string transactionHex)
        {
            Guard.NotEmpty(transactionHex, nameof(transactionHex));

            // TODO move this to a behavior to a dedicated interface
            // parse transaction
            Transaction transaction = Transaction.Parse(transactionHex);

            // replace this we a dedicated WalletBroadcast interface
            // in a fullnode implementation this will validate with the 
            // mempool and broadcast, in a lightnode this will push to 
            // the wallet and then broadcast (we might add some basic validation
            if (this.mempoolValidator == null)
            {
                this.ProcessTransaction(transaction);
            }
            else
            {
                var state = new MempoolValidationState(false);
                if (!this.mempoolValidator.AcceptToMemoryPool(state, transaction).GetAwaiter().GetResult())
                    return false;
                this.ProcessTransaction(transaction);
            }

            // broadcast to peers
            TxPayload payload = new TxPayload(transaction);
            foreach (var node in this.connectionManager.ConnectedNodes)
            {
                node.SendMessage(payload);
            }

            // we might want to create a behaviour that tracks how many times
            // the broadcast trasnactions was sent back to us by other peers
            return true;
        }

        /// <inheritdoc />
        public void RemoveBlocks(ChainedBlock fork)
        {
            Guard.NotNull(fork, nameof(fork));

            if (this.keysLookup == null)
            {
                this.LoadKeysLookup();
            }

            var allAddresses = this.keysLookup.Values;
            foreach (var address in allAddresses)
            {
                var toRemove = address.Transactions.Where(w => w.BlockHeight > fork.Height).ToList();
                foreach (var transactionData in toRemove)
                    address.Transactions.Remove(transactionData);
            }

            this.UpdateLastBlockSyncedHeight(fork);
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

            foreach (Transaction transaction in block.Transactions)
            {
                this.ProcessTransaction(transaction, chainedBlock.Height, block);
            }

            // update the wallets with the last processed block height
            this.UpdateLastBlockSyncedHeight(chainedBlock);
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
                this.LoadKeysLookup();
            }

            // check the outputs
            foreach (TxOut utxo in transaction.Outputs)
            {
                // check if the outputs contain one of our addresses
                if (this.keysLookup.TryGetValue(utxo.ScriptPubKey, out HdAddress pubKey))
                {
                    this.AddTransactionToWallet(hash, transaction.Time, transaction.Outputs.IndexOf(utxo), utxo.Value, utxo.ScriptPubKey, blockHeight, block);
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

                this.AddSpendingTransactionToWallet(hash, transaction.Time, paidoutto, tTx.Id, tTx.Index, blockHeight, block);
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
        private void AddTransactionToWallet(uint256 transactionHash, uint time, int index, Money amount, Script script,
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
                    ScriptPubKey = script
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

            // notify a transaction has been found
            this.TransactionFound?.Invoke(this, new TransactionFoundEventArgs(script, transactionHash));
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
        private void AddSpendingTransactionToWallet(uint256 transactionHash, uint time, IEnumerable<TxOut> paidToOutputs,
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
                    BlockHeight = blockHeight
                };

                spentTransaction.SpendingDetails = spendingDetails;
                spentTransaction.MerkleProof = null;
            }
            else // if this spending transaction is being comfirmed in a block
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

        private void OnTransactionFound(object sender, TransactionFoundEventArgs a)
        {
            foreach (Wallet wallet in this.Wallets)
            {
                foreach (var account in wallet.GetAccountsByCoinType(this.coinType))
                {
                    bool isChange;
                    if (account.ExternalAddresses.Any(address => address.ScriptPubKey == a.Script))
                    {
                        isChange = false;
                    }
                    else if (account.InternalAddresses.Any(address => address.ScriptPubKey == a.Script))
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
                    this.CreateAddressesInAccount(account, accountsToAdd, isChange);

                    // persists the address to the wallet file
                    this.SaveToFile(wallet);
                }
            }

            this.LoadKeysLookup();
        }

        /// <inheritdoc />
        public void DeleteWallet()
        {
            throw new NotImplementedException();
        }

        private IEnumerable<string> GetWalletFilesPaths()
        {
            // TODO look in user-chosen folder as well.
            // maybe the api can maintain a list of wallet paths it knows about
            var defaultFolderPath = this.dataFolder.WalletPath;

            // create the directory if it doesn't exist
            Directory.CreateDirectory(defaultFolderPath);
            return Directory.EnumerateFiles(defaultFolderPath, $"*.{WalletFileExtension}", SearchOption.TopDirectoryOnly);
        }

        /// <inheritdoc />
        public void SaveToFile()
        {
            foreach (var wallet in this.Wallets)
            {
                this.SaveToFile(wallet);
            }
        }

        /// <inheritdoc />
        public void SaveToFile(Wallet wallet)
        {
            Guard.NotNull(wallet, nameof(wallet));

            var walletfile = Path.Combine(this.dataFolder.WalletPath, $"{wallet.Name}.{WalletFileExtension}");
            File.WriteAllText(walletfile, JsonConvert.SerializeObject(wallet, Formatting.Indented));
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

            // update the wallets with the last processed block height
            foreach (var accountRoot in wallet.AccountsRoot.Where(a => a.CoinType == this.coinType))
            {
                accountRoot.LastBlockSyncedHeight = chainedBlock.Height;
                accountRoot.LastBlockSyncedHash = chainedBlock.HashBlock;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // safely persist the wallets to the file system before disposing
            foreach (var wallet in this.Wallets)
            {
                this.SaveToFile(wallet);
            }
        }

        /// <summary>
        /// Generates the wallet file.
        /// </summary>
        /// <param name="password">The password used to encrypt sensitive info.</param>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="extendedKey">The root key used to generate keys.</param>
        /// <param name="creationTime">The time this wallet was created.</param>
        /// <returns></returns>
        /// <exception cref="System.NotSupportedException"></exception>
        private Wallet GenerateWalletFile(string password, string name, ExtKey extendedKey, DateTimeOffset? creationTime = null)
        {
            string walletFilePath = Path.Combine(this.dataFolder.WalletPath, $"{name}.{WalletFileExtension}");

            if (File.Exists(walletFilePath))
                throw new InvalidOperationException($"Wallet already exists at {walletFilePath}");

            Wallet walletFile = new Wallet
            {
                Name = name,
                EncryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.network).ToWif(),
                ChainCode = extendedKey.ChainCode,
                CreationTime = creationTime ?? DateTimeOffset.Now,
                Network = this.network,
                AccountsRoot = new List<AccountRoot> { new AccountRoot { Accounts = new List<HdAccount>(), CoinType = this.coinType } },
            };

            // create a folder if none exists and persist the file
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(walletFilePath)));
            File.WriteAllText(walletFilePath, JsonConvert.SerializeObject(walletFile, Formatting.Indented));

            return walletFile;
        }

        /// <summary>
        /// Gets the wallet located at the specified path.
        /// </summary>
        /// <param name="walletFilePath">The wallet file path.</param>
        /// <returns></returns>
        /// <exception cref="System.IO.FileNotFoundException"></exception>
        private Wallet DeserializeWallet(string walletFilePath)
        {
            if (!File.Exists(walletFilePath))
                throw new FileNotFoundException($"No wallet file found at {walletFilePath}");

            // load the file from the local system
            return JsonConvert.DeserializeObject<Wallet>(File.ReadAllText(walletFilePath));
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

        private AccountRoot GetAccounts(Wallet wallet)
        {
            return wallet.AccountsRoot.Single(a => a.CoinType == this.coinType);
        }
        
        /// <summary>
        /// Loads the keys and transactions we're tracking in memory for faster lookups.
        /// </summary>
        /// <returns></returns>
        internal void LoadKeysLookup()
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

        public string[] GetWallets()
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

    public class TransactionDetails
    {
        public uint256 Hash { get; set; }

        public int? Index { get; set; }

        public Money Amount { get; internal set; }

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

    /// <summary>
    /// Represents an UTXO that keeps a reference to <see cref="HdAddress"/> and <see cref="HdAccount"/>.
    /// </summary>
    /// <remarks>
    /// This is useful when an UTXO needs access to its HD properties like the HD path when reconstructing a private key.
    /// </remarks>
    public class UnspentOutputReference
    {
        /// <summary>
        /// The account associated with this UTXO
        /// </summary>
        public HdAccount Account { get; set; }
 
        /// <summary>
        /// The address associated with this UTXO
        /// </summary>
        public HdAddress Address { get; set; }

        /// <summary>
        /// The transaction representing the UTXO.
        /// </summary>
        public TransactionData Transaction { get; set; }

        /// <summary>
        /// Convert the <see cref="TransactionData"/> to an <see cref="OutPoint"/>
        /// </summary>
        /// <returns>The corresponding <see cref="OutPoint"/>.</returns>
        public OutPoint ToOutPoint()
        {
            return new OutPoint(this.Transaction.Id, (uint) this.Transaction.Index);
        }
    }

    /// <summary>
    /// Represent a high level account container that hold's all its <see cref="UnspentOutputReference"/>.
    /// </summary>
    public class UnspentAccountReference
    {
        public UnspentAccountReference()
        {
            this.UnspentOutputs = new List<UnspentOutputReference>();
        }

        /// <summary>
        /// The UTXO's associated with this account.
        /// </summary>
        public List<UnspentOutputReference> UnspentOutputs { get; set; }
    }
}