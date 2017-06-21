using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.MemoryPool;
using Transaction = NBitcoin.Transaction;

namespace Stratis.Bitcoin.Wallet
{
    /// <summary>
    /// A manager providing operations on wallets.
    /// </summary>
    public class WalletManager : IWalletManager
    {
        public List<Wallet> Wallets { get; }

        private const int UnusedAddressesBuffer = 20;
        private const int WalletRecoveryAccountsCount = 3;
        private const int WalletCreationAccountsCount = 2;
        private const string WalletFileExtension = "wallet.json";

        private readonly CoinType coinType;
        private readonly Network network;
        private readonly ConnectionManager connectionManager;
        private readonly ConcurrentChain chain;
        private readonly NodeSettings settings;
        private readonly DataFolder dataFolder;
        private readonly IWalletFeePolicy walletFeePolicy;
        private readonly MempoolValidator mempoolValidator;
        private readonly ILogger logger;

        public uint256 WalletTipHash { get; set; }

        //TODO: a second lookup dictionary is proposed to lookup for spent outputs
        // every time we find a trx that credits we need to add it to this lookup
        // private Dictionary<OutPoint, TransactionData> outpointLookup;

        private Dictionary<Script, HdAddress> keysLookup;

        /// <summary>
        /// Occurs when a transaction is found.
        /// </summary>
        public event EventHandler<TransactionFoundEventArgs> TransactionFound;

        public WalletManager(ILoggerFactory loggerFactory, ConnectionManager connectionManager, Network network, ConcurrentChain chain,
            NodeSettings settings, DataFolder dataFolder, IWalletFeePolicy walletFeePolicy, MempoolValidator mempoolValidator = null) // mempool does not exist in a light wallet
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Wallets = new List<Wallet>();

            this.connectionManager = connectionManager;
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chain = chain;
            this.settings = settings;
            this.dataFolder = dataFolder;
            this.walletFeePolicy = walletFeePolicy;
            this.mempoolValidator = mempoolValidator;

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
        }

        /// <inheritdoc />
        public Mnemonic CreateWallet(string password, string name, string passphrase = null)
        {
            // for now the passphrase is set to be the password by default.
            if (passphrase == null)
            {
                passphrase = password;
            }

            // generate the root seed used to generate keys from a mnemonic picked at random 
            // and a passphrase optionally provided by the user
            Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            ExtKey extendedKey = mnemonic.DeriveExtKey(passphrase);

            // create a wallet file 
            Wallet wallet = this.GenerateWalletFile(password, name, extendedKey);

            // generate multiple accounts and addresses from the get-go
            for (int i = 0; i < WalletCreationAccountsCount; i++)
            {
                HdAccount account = CreateNewAccount(wallet, password);
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
            var walletFilePath = Path.Combine(this.dataFolder.WalletPath, $"{name}.{WalletFileExtension}");

            // load the file from the local system
            Wallet wallet = this.DeserializeWallet(walletFilePath);

            this.Load(wallet);
            return wallet;
        }

        /// <inheritdoc />
        public Wallet RecoverWallet(string password, string name, string mnemonic, DateTime creationTime, string passphrase = null)
        {
            // for now the passphrase is set to be the password by default.
            if (passphrase == null)
            {
                passphrase = password;
            }

            // generate the root seed used to generate keys
            ExtKey extendedKey = (new Mnemonic(mnemonic)).DeriveExtKey(passphrase);

            // create a wallet file 
            Wallet wallet = this.GenerateWalletFile(password, name, extendedKey, creationTime);

            // generate multiple accounts and addresses from the get-go
            for (int i = 0; i < WalletRecoveryAccountsCount; i++)
            {
                HdAccount account = CreateNewAccount(wallet, password);
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
            Wallet wallet = this.GetWalletByName(walletName);

            return this.GetUnusedAccount(wallet, password);
        }

        /// <inheritdoc />
        public HdAccount GetUnusedAccount(Wallet wallet, string password)
        {
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
            var newAccount = this.CreateNewAccount(wallet, password);

            // save the changes to the file
            this.SaveToFile(wallet);
            return newAccount;
        }

        /// <inheritdoc />
        public HdAccount CreateNewAccount(Wallet wallet, string password)
        {
            // get the accounts for this type of coin
            var accounts = wallet.AccountsRoot.Single(a => a.CoinType == this.coinType).Accounts.ToList();

            int newAccountIndex = 0;
            if (accounts.Any())
            {
                newAccountIndex = accounts.Max(a => a.Index) + 1;
            }

            // get the extended pub key used to generate addresses for this account
            var privateKey = Key.Parse(wallet.EncryptedSeed, password, wallet.Network);
            var seedExtKey = new ExtKey(privateKey, wallet.ChainCode);
            var accountHdPath = $"m/44'/{(int)this.coinType}'/{newAccountIndex}'";
            KeyPath keyPath = new KeyPath(accountHdPath);
            ExtKey accountExtKey = seedExtKey.Derive(keyPath);
            ExtPubKey accountExtPubKey = accountExtKey.Neuter();

            var newAccount = new HdAccount
            {
                Index = newAccountIndex,
                ExtendedPubKey = accountExtPubKey.ToString(wallet.Network),
                ExternalAddresses = new List<HdAddress>(),
                InternalAddresses = new List<HdAddress>(),
                Name = $"account {newAccountIndex}",
                HdPath = accountHdPath,
                CreationTime = DateTimeOffset.Now
            };

            accounts.Add(newAccount);
            wallet.AccountsRoot.Single(a => a.CoinType == this.coinType).Accounts = accounts;

            return newAccount;
        }

        /// <inheritdoc />
        public HdAddress GetUnusedAddress(string walletName, string accountName)
        {
            Wallet wallet = this.GetWalletByName(walletName);

            // get the account
            HdAccount account = wallet.AccountsRoot.Single(a => a.CoinType == this.coinType).GetAccountByName(accountName);

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
        public IEnumerable<HdAddress> GetHistory(string walletName)
        {
            Wallet wallet = this.GetWalletByName(walletName);

            return this.GetHistory(wallet);
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetHistory(Wallet wallet)
        {
            var accounts = wallet.GetAccountsByCoinType(this.coinType).ToList();

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
                var pubkey = this.GenerateAddress(account.ExtendedPubKey, i, isChange, this.network);
                BitcoinPubKeyAddress address = pubkey.GetAddress(this.network);

                // add address details
                addresses.Add(new HdAddress
                {
                    Index = i,
                    HdPath = CreateBip44Path(account.GetCoinType(), account.Index, i, isChange),
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
            Wallet wallet = this.GetWalletByName(walletName);
            return wallet;
        }

        /// <inheritdoc />
        public IEnumerable<HdAccount> GetAccounts(string walletName)
        {
            Wallet wallet = this.GetWalletByName(walletName);

            return wallet.GetAccountsByCoinType(this.coinType);
        }

        public int LastBlockHeight()
        {
            if (!this.Wallets.Any())
            {
                return this.chain.Tip.Height;
            }

            return this.Wallets.Min(w => w.AccountsRoot.Single(a => a.CoinType == this.coinType).LastBlockSyncedHeight) ?? 0;
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

            return this.Wallets.Select(w => w.AccountsRoot.Single(a => a.CoinType == this.coinType))
                       .OrderBy(o => o.LastBlockSyncedHeight).FirstOrDefault()?.LastBlockSyncedHash ?? this.network.GenesisHash;
        }

        /// <inheritdoc />
        public List<UnspentInfo> GetSpendableTransactions(int confirmations = 0)
        {
            var outs = new List<UnspentInfo>();
            var accounts = this.Wallets.SelectMany(wallet => wallet.AccountsRoot.Single(a => a.CoinType == this.coinType).Accounts);

            var currentHeight = this.chain.Tip.Height;

            // this will take all the spendable coins 
            // and keep the reference to the HDAddress
            // so later the private key can be calculated 
            // for the given unspent outputs 

            foreach (var account in accounts)
            {
                foreach (var externalAddress in account.ExternalAddresses)
                {
                    var unspent = externalAddress.UnspentTransactions().Where(a => currentHeight - (a.BlockHeight ?? currentHeight) >= confirmations).ToList();
                    if (unspent.Any())
                    {
                        outs.Add(new UnspentInfo
                        {
                            Account = account,
                            Address = externalAddress,
                            Transactions = unspent
                        });
                    }

                }
                foreach (var internalAddress in account.InternalAddresses)
                {
                    var unspent = internalAddress.UnspentTransactions().Where(a => currentHeight - (a.BlockHeight ?? currentHeight) >= confirmations).ToList();
                    if (unspent.Any())
                    {
                        outs.Add(new UnspentInfo
                        {
                            Account = account,
                            Address = internalAddress,
                            Transactions = unspent
                        });
                    }
                }
            }

            return outs;
        }

        /// <inheritdoc />
        public ISecret GetKeyForAddress(string password, HdAddress address)
        {
            // TODO: can we have more then one wallet per coins?
            var walletTree = this.Wallets.First();
            // get extended private key
            var privateKey = Key.Parse(walletTree.EncryptedSeed, password, walletTree.Network);
            var seedExtKey = new ExtKey(privateKey, walletTree.ChainCode);
            ExtKey addressExtKey = seedExtKey.Derive(new KeyPath(address.HdPath));
            BitcoinExtKey addressPrivateKey = addressExtKey.GetWif(walletTree.Network);
            return addressPrivateKey;
        }

        /// <inheritdoc />
        public (string hex, uint256 transactionId, Money fee) BuildTransaction(string walletName, string accountName, string password, string destinationAddress, Money amount, string feeType, int minConfirmations)
        {
            if (amount == Money.Zero)
            {
                throw new WalletException($"Cannot send transaction with 0 {this.coinType}");
            }

            // get the wallet and the account
            Wallet wallet = this.GetWalletByName(walletName);
            HdAccount account = wallet.AccountsRoot.Single(a => a.CoinType == this.coinType).GetAccountByName(accountName);

            // get script destination address
            Script destinationScript = null;
            try
            {
                destinationScript = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new BitcoinPubKeyAddress(destinationAddress, wallet.Network));
            }
            catch
            {
                throw new WalletException("Invalid address.");
            }

            // get a list of transactions outputs that have not been spent
            var spendableTransactions = account.GetSpendableTransactions().ToList();

            // remove whats under min confirmations
            var currentHeight = this.chain.Height;
            spendableTransactions = spendableTransactions.Where(s => currentHeight - s.BlockHeight >= minConfirmations).ToList();

            // get total spendable balance in the account.
            var balance = spendableTransactions.Sum(t => t.Amount);

            // make sure we have enough funds
            if (balance < amount)
            {
                throw new WalletException("Not enough funds.");
            }

            // calculate which addresses needs to be used as well as the fee to be charged
            var calculationResult = this.CalculateFees(spendableTransactions, amount, 5);

            // get extended private key
            var privateKey = Key.Parse(wallet.EncryptedSeed, password, wallet.Network);
            var seedExtKey = new ExtKey(privateKey, wallet.ChainCode);

            var signingKeys = new HashSet<ISecret>();
            var coins = new List<Coin>();
            foreach (var transactionToUse in calculationResult.transactionsToUse)
            {
                var address = account.FindAddressesForTransaction(t => t.Id == transactionToUse.Id && t.Index == transactionToUse.Index && t.Amount > 0).Single();
                ExtKey addressExtKey = seedExtKey.Derive(new KeyPath(address.HdPath));
                BitcoinExtKey addressPrivateKey = addressExtKey.GetWif(wallet.Network);
                signingKeys.Add(addressPrivateKey);

                coins.Add(new Coin(transactionToUse.Id, (uint)transactionToUse.Index, transactionToUse.Amount, transactionToUse.ScriptPubKey));
            }

            // get address to send the change to
            var changeAddress = account.GetFirstUnusedChangeAddress();

            // build transaction
            var builder = new TransactionBuilder();
            Transaction tx = builder
                .AddCoins(coins)
                .AddKeys(signingKeys.ToArray())
                .Send(destinationScript, amount)
                .SetChange(changeAddress.ScriptPubKey)
                .SendFees(calculationResult.fee)
                .BuildTransaction(true);

            if (!builder.Verify(tx))
            {
                throw new WalletException("Could not build transaction, please make sure you entered the correct data.");
            }

            return (tx.ToHex(), tx.GetHash(), calculationResult.fee);
        }

        /// <summary>
        /// Calculates which outputs are to be used in the transaction, as well as the fees that will be charged.
        /// </summary>
        /// <param name="spendableTransactions">The transactions with unspent funds.</param>
        /// <param name="amount">The amount to be sent.</param>
        /// <returns>The collection of transactions to be used and the fee to be charged</returns>
        private (List<TransactionData> transactionsToUse, Money fee) CalculateFees(IEnumerable<TransactionData> spendableTransactions, Money amount, int targetConfirmations)
        {
            Money fee = 0;
            List<TransactionData> transactionsToUse = new List<TransactionData>();
            var inputCount = 1;
            foreach (var transaction in spendableTransactions)
            {
                fee = this.walletFeePolicy.GetMinimumFee(inputCount * 180 + 74, targetConfirmations);

                transactionsToUse.Add(transaction);
                if (transactionsToUse.Sum(t => t.Amount) >= amount + fee)
                {
                    break;
                }
            }

            return (transactionsToUse, fee);
        }

        /// <inheritdoc />
        public bool SendTransaction(string transactionHex)
        {
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
                {
                    return false;
                }
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
            var allAddresses = this.keysLookup.Values;
            foreach (var address in allAddresses)
            {
                var toremove = address.Transactions.Where(w => w.BlockHeight > fork.Height).ToList();
                foreach (var transactionData in toremove)
                    address.Transactions.Remove(transactionData);
            }

            this.UpdateLastBlockSyncedHeight(fork);
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block, ChainedBlock chainedBlock)
        {
            this.logger.LogDebug($"block notification - height: {chainedBlock.Height}, hash: {block.Header.GetHash()}, coin: {this.coinType}");
            
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

            if (this.Wallets.Any())
            {
                foreach (Transaction transaction in block.Transactions)
                {
                    this.ProcessTransaction(transaction, chainedBlock.Height, block);
                }
            }

            // update the wallets with the last processed block height
            this.UpdateLastBlockSyncedHeight(chainedBlock);
        }

        /// <inheritdoc />
        public void ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null)
        {
            var hash = transaction.GetHash();
            this.logger.LogDebug($"transaction received - hash: {hash}, coin: {this.coinType}");

            // check the outputs
            foreach (TxOut utxo in transaction.Outputs)
            {
                HdAddress pubKey;
                // check if the outputs contain one of our addresses
                if (this.keysLookup.TryGetValue(utxo.ScriptPubKey, out pubKey))
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
        private void AddTransactionToWallet(uint256 transactionHash, uint time, int? index, Money amount, Script script,
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
                    Id = transactionHash,
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? time),
                    Index = index,
                    ScriptPubKey = script
                };

                // add the Merkle proof to the (non-spending) transaction
                if (block != null)
                {
                    newTransaction.MerkleProof = this.CreateMerkleProof(block, transactionHash);
                }

                addressTransactions.Add(newTransaction);
            }
            else
            {
                // update the block height
                if (foundTransaction.BlockHeight == null && blockHeight != null)
                {
                    foundTransaction.BlockHeight = blockHeight;
                }

                // update the block time
                if (block != null)
                {
                    foundTransaction.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
                }

                // add the Merkle proof now that the transaction is confirmed in a block
                if (foundTransaction.MerkleProof == null)
                {
                    foundTransaction.MerkleProof = this.CreateMerkleProof(block, transactionHash);
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

        private MerkleProof CreateMerkleProof(Block block, uint256 transactionHash)
        {
            MerkleBlock merkleBlock = new MerkleBlock(block, new[] { transactionHash });

            return new MerkleProof
            {
                MerkleRoot = block.Header.HashMerkleRoot,
                MerklePath = merkleBlock.PartialMerkleTree.Hashes
            };
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
        public void SaveToFile(Wallet wallet)
        {
            var walletfile = Path.Combine(this.dataFolder.WalletPath, $"{wallet.Name}.{WalletFileExtension}");
            File.WriteAllText(walletfile, JsonConvert.SerializeObject(wallet, Formatting.Indented));
        }

        /// <inheritdoc />
        public void UpdateLastBlockSyncedHeight(ChainedBlock chainedBlock)
        {
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
        public void SaveToFile()
        {
            foreach (var wallet in this.Wallets)
            {
                this.SaveToFile(wallet);
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
                Network = network,
                AccountsRoot = new List<AccountRoot> {
                    new AccountRoot { Accounts = new List<HdAccount>(), CoinType = CoinType.Bitcoin },
                    new AccountRoot { Accounts = new List<HdAccount>(), CoinType = CoinType.Testnet },
                    new AccountRoot { Accounts = new List<HdAccount>(), CoinType = CoinType.Stratis} },
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
            if (this.Wallets.Any(w => w.Name == wallet.Name))
            {
                return;
            }

            this.Wallets.Add(wallet);
        }

        private PubKey GenerateAddress(string accountExtPubKey, int index, bool isChange, Network network)
        {
            int change = isChange ? 1 : 0;
            KeyPath keyPath = new KeyPath($"{change}/{index}");
            ExtPubKey extPubKey = ExtPubKey.Parse(accountExtPubKey).Derive(keyPath);
            return extPubKey.PubKey;
        }

        /// <summary>
        /// Creates the bip44 path.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <param name="accountIndex">Index of the account.</param>
        /// <param name="addressIndex">Index of the address.</param>
        /// <param name="isChange">if set to <c>true</c> [is change].</param>
        /// <returns></returns>
        public static string CreateBip44Path(CoinType coinType, int accountIndex, int addressIndex, bool isChange = false)
        {
            //// populate the items according to the BIP44 path 
            //// [m/purpose'/coin_type'/account'/change/address_index]

            int change = isChange ? 1 : 0;
            return $"m/44'/{(int)coinType}'/{accountIndex}'/{change}/{addressIndex}";
        }

        /// <summary>
        /// Loads the keys and transactions we're tracking in memory for faster lookups.
        /// </summary>
        /// <returns></returns>
        private void LoadKeysLookup()
        {
            this.keysLookup = new Dictionary<Script, HdAddress>();
            foreach (var wallet in this.Wallets)
            {
                var accounts = wallet.GetAccountsByCoinType(this.coinType);
                foreach (var account in accounts)
                {
                    var addresses = account.ExternalAddresses.Concat(account.InternalAddresses);
                    foreach (var address in addresses)
                    {
                        this.keysLookup.Add(address.ScriptPubKey, address);
                        if (address.Pubkey != null)
                            this.keysLookup.Add(address.Pubkey, address);
                    }
                }
            }
        }

        /// <summary>
        /// Gets a wallet given its name.
        /// </summary>
        /// <param name="walletName">The name of the wallet to get.</param>
        /// <returns>A wallet or null if it doesn't exist</returns>
        private Wallet GetWalletByName(string walletName)
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

    public class UnspentInfo
    {
        public HdAccount Account { get; set; }

        public HdAddress Address { get; set; }

        public List<TransactionData> Transactions { get; set; }
    }

}