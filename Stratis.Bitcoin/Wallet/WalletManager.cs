using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Stratis.Bitcoin.Wallet.Helpers;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Newtonsoft.Json;
using Stratis.Bitcoin.Connection;
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

        private readonly CoinType coinType;

        private readonly Network network;

        private readonly ConnectionManager connectionManager;

        private readonly ConcurrentChain chain;

        private ChainedBlock LastBlock;

        //TODO: a second lookup dictionary is proposed to lookup for spent outputs
        // every time we find a trx that credits we need to add it to this lookup
        // private Dictionary<OutPoint, TransactionData> outpointLookup;

        private Dictionary<Script, HdAddress> keysLookup;

        private readonly ILogger logger;

        /// <summary>
        /// Occurs when a transaction is found.
        /// </summary>
        public event EventHandler<TransactionFoundEventArgs> TransactionFound;

        public WalletManager(ILoggerFactory loggerFactory, ConnectionManager connectionManager, Network network, ConcurrentChain chain)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Wallets = new List<Wallet>();

            // find wallets and load them in memory
            foreach (var path in this.GetWalletFilesPaths())
            {
                this.Load(this.DeserializeWallet(path));
            }

            this.connectionManager = connectionManager;
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chain = chain;

            // load data in memory for faster lookups
            this.LoadKeysLookup();

            // register events
            this.TransactionFound += this.OnTransactionFound;
        }

        public void SetBlock()
        {
            // find the last chain block.
            this.LastBlock = this.chain.GetBlock(this.LastBlockHash());

            // TODO: fix reorg logic
            if (this.LastBlock == null)
                throw new WalletException("Reorg on startup");
        }

        /// <inheritdoc />
        public Mnemonic CreateWallet(string password, string folderPath, string name, string network, string passphrase = null)
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

            Network coinNetwork = WalletHelpers.GetNetwork(network);

            // create a wallet file 
            Wallet wallet = this.GenerateWalletFile(password, folderPath, name, coinNetwork, extendedKey);

            // generate multiple accounts and addresses from the get-go
            for (int i = 0; i < WalletCreationAccountsCount; i++)
            {
                HdAccount account = CreateNewAccount(wallet, this.coinType, password);
                this.CreateAddressesInAccount(account, coinNetwork, UnusedAddressesBuffer);
                this.CreateAddressesInAccount(account, coinNetwork, UnusedAddressesBuffer, true);
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
        public Wallet LoadWallet(string password, string folderPath, string name)
        {
            string walletFilePath = Path.Combine(folderPath, $"{name}.json");

            // load the file from the local system
            Wallet wallet = this.DeserializeWallet(walletFilePath);

            this.Load(wallet);
            return wallet;
        }

        /// <inheritdoc />
        public Wallet RecoverWallet(string password, string folderPath, string name, string network, string mnemonic, DateTime creationTime, string passphrase = null)
        {
            // for now the passphrase is set to be the password by default.
            if (passphrase == null)
            {
                passphrase = password;
            }

            // generate the root seed used to generate keys
            ExtKey extendedKey = (new Mnemonic(mnemonic)).DeriveExtKey(passphrase);

            Network coinNetwork = WalletHelpers.GetNetwork(network);

            // create a wallet file 
            Wallet wallet = this.GenerateWalletFile(password, folderPath, name, coinNetwork, extendedKey, creationTime);

            // generate multiple accounts and addresses from the get-go
            for (int i = 0; i < WalletRecoveryAccountsCount; i++)
            {
                HdAccount account = CreateNewAccount(wallet, this.coinType, password);
                this.CreateAddressesInAccount(account, coinNetwork, UnusedAddressesBuffer);
                this.CreateAddressesInAccount(account, coinNetwork, UnusedAddressesBuffer, true);
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
        public HdAccount GetUnusedAccount(string walletName, CoinType coinType, string password)
        {
            Wallet wallet = this.GetWalletByName(walletName);

            return this.GetUnusedAccount(wallet, coinType, password);
        }

        /// <inheritdoc />
        public HdAccount GetUnusedAccount(Wallet wallet, CoinType coinType, string password)
        {
            // get the accounts root for this type of coin
            var accountsRoot = wallet.AccountsRoot.Single(a => a.CoinType == coinType);

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
            var newAccount = this.CreateNewAccount(wallet, coinType, password);

            // save the changes to the file
            this.SaveToFile(wallet);
            return newAccount;
        }

        /// <inheritdoc />
        public HdAccount CreateNewAccount(Wallet wallet, CoinType coinType, string password)
        {
            // get the accounts for this type of coin
            var accounts = wallet.AccountsRoot.Single(a => a.CoinType == coinType).Accounts.ToList();

            int newAccountIndex = 0;
            if (accounts.Any())
            {
                newAccountIndex = accounts.Max(a => a.Index) + 1;
            }

            // get the extended pub key used to generate addresses for this account
            var privateKey = Key.Parse(wallet.EncryptedSeed, password, wallet.Network);
            var seedExtKey = new ExtKey(privateKey, wallet.ChainCode);
            var accountHdPath = $"m/44'/{(int)coinType}'/{newAccountIndex}'";
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
            wallet.AccountsRoot.Single(a => a.CoinType == coinType).Accounts = accounts;

            return newAccount;
        }

        /// <inheritdoc />
        public string GetUnusedAddress(string walletName, CoinType coinType, string accountName)
        {
            Wallet wallet = this.GetWalletByName(walletName);

            // get the account
            HdAccount account = wallet.AccountsRoot.Single(a => a.CoinType == coinType).GetAccountByName(accountName);

            // validate address creation
            if (account.ExternalAddresses.Any())
            {
                // check last created address contains transactions.
                var firstUnusedExternalAddress = account.GetFirstUnusedReceivingAddress();
                if (firstUnusedExternalAddress != null)
                {
                    return firstUnusedExternalAddress.Address;
                }
            }

            // creates an address
            this.CreateAddressesInAccount(account, wallet.Network, 1);

            // persists the address to the wallet file
            this.SaveToFile(wallet);

            // adds the address to the list of tracked addresses
            this.LoadKeysLookup();
            return account.GetFirstUnusedReceivingAddress().Address;
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetHistoryByCoinType(string walletName, CoinType coinType)
        {
            Wallet wallet = this.GetWalletByName(walletName);

            return this.GetHistoryByCoinType(wallet, coinType);
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetHistoryByCoinType(Wallet wallet, CoinType coinType)
        {
            var accounts = wallet.GetAccountsByCoinType(coinType).ToList();

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
        /// <param name="network">The network.</param>
        /// <param name="addressesQuantity">The number of addresses to create.</param>
        /// <param name="isChange">Whether the addresses added are change (internal) addresses or receiving (external) addresses.</param>
        /// <returns>A list of addresses in Base58.</returns>
        private List<string> CreateAddressesInAccount(HdAccount account, Network network, int addressesQuantity, bool isChange = false)
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
                var pubkey = this.GenerateAddress(account.ExtendedPubKey, i, isChange, network);
                BitcoinPubKeyAddress address = pubkey.GetAddress(network);

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
        public IEnumerable<HdAccount> GetAccountsByCoinType(string walletName, CoinType coinType)
        {
            Wallet wallet = this.GetWalletByName(walletName);

            return wallet.GetAccountsByCoinType(coinType);
        }

        public int LastBlockHeight()
        {
            if (!this.Wallets.Any())
            {
                return this.chain.Tip.Height;
            }

            return this.Wallets.Min(w => w.AccountsRoot.Single(a => a.CoinType == this.coinType).LastBlockSyncedHeight) ?? 0;
        }

        public uint256 LastBlockHash()
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
        public (string hex, uint256 transactionId, Money fee) BuildTransaction(string walletName, string accountName, CoinType coinType, string password, string destinationAddress, Money amount, string feeType, bool allowUnconfirmed)
        {
            if (amount == Money.Zero)
            {
                throw new WalletException($"Cannot send transaction with 0 {this.coinType}");
            }

            // get the wallet and the account
            Wallet wallet = this.GetWalletByName(walletName);
            HdAccount account = wallet.AccountsRoot.Single(a => a.CoinType == coinType).GetAccountByName(accountName);

            // get a list of transactions outputs that have not been spent
            IEnumerable<TransactionData> spendableTransactions = account.GetSpendableTransactions();

            // get total spendable balance in the account.
            var balance = spendableTransactions.Sum(t => t.Amount);

            // make sure we have enough funds
            if (balance < amount)
            {
                throw new WalletException("Not enough funds.");
            }

            // calculate which addresses needs to be used as well as the fee to be charged
            var calculationResult = this.CalculateFees(spendableTransactions, amount);

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

            // get script destination address
            Script destinationScript = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new BitcoinPubKeyAddress(destinationAddress, wallet.Network));

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
        private (List<TransactionData> transactionsToUse, Money fee) CalculateFees(IEnumerable<TransactionData> spendableTransactions, Money amount)
        {
            // TODO make this a bit smarter!            
            List<TransactionData> transactionsToUse = new List<TransactionData>();
            foreach (var transaction in spendableTransactions)
            {
                transactionsToUse.Add(transaction);
                if (transactionsToUse.Sum(t => t.Amount) >= amount)
                {
                    break;
                }
            }

            Money fee = new Money(new decimal(0.001), MoneyUnit.BTC);
            return (transactionsToUse, fee);
        }

        /// <inheritdoc />
        public bool SendTransaction(string transactionHex)
        {
            // TODO move this to a behavior on the full node
            // parse transaction
            Transaction transaction = Transaction.Parse(transactionHex);
            TxPayload payload = new TxPayload(transaction);

            foreach (var node in this.connectionManager.ConnectedNodes)
            {
                node.SendMessage(payload);
            }

            return true;
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block)
        {
            var chainedBlock = this.chain.GetBlock(block.GetHash());

            this.logger.LogDebug($"block notification - height: {chainedBlock.Height}, hash: {block.Header.GetHash()}, coin: {this.coinType}");

            // TODO: fix reorg logic
            // check for reorg
            if (block.Header.HashPrevBlock != this.LastBlock.HashBlock)
                throw new WalletException("Reorg");


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
            this.logger.LogDebug($"transaction received - hash: {transaction.GetHash()}, coin: {this.coinType}");

            var hash = transaction.GetHash().ToString();
            // check the outputs
            foreach (TxOut utxo in transaction.Outputs)
            {
                HdAddress pubKey;
                // check if the outputs contain one of our addresses
                if (this.keysLookup.TryGetValue(utxo.ScriptPubKey, out pubKey))
                {
                    this.AddTransactionToWallet(transaction.GetHash(), transaction.Time, transaction.Outputs.IndexOf(utxo), utxo.Value, utxo.ScriptPubKey, blockHeight, block);
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

                AddTransactionToWallet(transaction.GetHash(), transaction.Time, null, -tTx.Amount, keyToSpend, blockHeight, block, tTx.Id, tTx.Index, paidoutto);
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
        /// <param name="spendingTransactionId">The id of the transaction containing the output being spent, if this is a spending transaction.</param>
        /// <param name="spendingTransactionIndex">The index of the output in the transaction being referenced, if this is a spending transaction.</param>
        private void AddTransactionToWallet(uint256 transactionHash, uint time, int? index, Money amount, Script script,
            int? blockHeight = null, Block block = null, uint256 spendingTransactionId = null,
            int? spendingTransactionIndex = null, IEnumerable<TxOut> paidToOutputs = null)
        {
            // get the collection of transactions to add to.
            this.keysLookup.TryGetValue(script, out HdAddress address);

            var isSpendingTransaction = paidToOutputs != null && paidToOutputs.Any();
            var trans = address.Transactions;

            // check if a similar UTXO exists or not (same transaction id and same index)
            // new UTXOs are added, existing ones are updated
            var foundTransaction = trans.FirstOrDefault(t => t.Id == transactionHash && t.Index == index);
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
                if (block != null && !isSpendingTransaction)
                {
                    newTransaction.MerkleProof = this.CreateMerkleProof(block, transactionHash);
                }

                // if this is a spending transaction, keep a record of the payments made out to other scripts.
                if (isSpendingTransaction)
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

                    newTransaction.Payments = payments;

                    // mark the transaction spent by this transaction as such
                    var transactions = this.keysLookup.Values.Distinct().SelectMany(v => v.Transactions)
                        .Where(t => t.Id == spendingTransactionId);
                    if (transactions.Any())
                    {
                        var spentTransaction = transactions.Single(t => t.Index == spendingTransactionIndex);
                        spentTransaction.SpentInTransaction = transactionHash;
                        spentTransaction.MerkleProof = null;
                    }
                }

                trans.Add(newTransaction);
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
                if (!isSpendingTransaction && foundTransaction.MerkleProof == null)
                {
                    foundTransaction.MerkleProof = this.CreateMerkleProof(block, transactionHash);
                }
            }

            // notify a transaction has been found
            this.TransactionFound?.Invoke(this, new TransactionFoundEventArgs(script, transactionHash));
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
                    this.CreateAddressesInAccount(account, wallet.Network, accountsToAdd, isChange);

                    // persists the address to the wallet file
                    this.SaveToFile(wallet);
                }
            }

            this.LoadKeysLookup();
        }

        /// <inheritdoc />
        public void DeleteWallet(string walletFilePath)
        {
            File.Delete(walletFilePath);
        }

        /// <inheritdoc />
        public void SaveToFile(Wallet wallet)
        {
            File.WriteAllText(wallet.WalletFilePath, JsonConvert.SerializeObject(wallet, Formatting.Indented));
        }

        /// <inheritdoc />
        public void UpdateLastBlockSyncedHeight(ChainedBlock chainedBlock)
        {
            // update the wallets with the last processed block height
            foreach (var wallet in this.Wallets)
            {
                this.UpdateLastBlockSyncedHeight(wallet, chainedBlock);
            }
        }

        /// <inheritdoc />
        public void UpdateLastBlockSyncedHeight(Wallet wallet, ChainedBlock chainedBlock)
        {
            this.LastBlock = chainedBlock;

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
        /// <param name="folderPath">The folder where the wallet will be generated.</param>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="network">The network this wallet is for.</param>
        /// <param name="extendedKey">The root key used to generate keys.</param>
        /// <param name="creationTime">The time this wallet was created.</param>
        /// <returns></returns>
        /// <exception cref="System.NotSupportedException"></exception>
        private Wallet GenerateWalletFile(string password, string folderPath, string name, Network network, ExtKey extendedKey, DateTimeOffset? creationTime = null)
        {
            string walletFilePath = Path.Combine(folderPath, $"{name}.json");

            if (File.Exists(walletFilePath))
                throw new InvalidOperationException($"Wallet already exists at {walletFilePath}");

            Wallet walletFile = new Wallet
            {
                Name = name,
                EncryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, network).ToWif(),
                ChainCode = extendedKey.ChainCode,
                CreationTime = creationTime ?? DateTimeOffset.Now,
                Network = network,
                AccountsRoot = new List<AccountRoot> {
                    new AccountRoot { Accounts = new List<HdAccount>(), CoinType = CoinType.Bitcoin },
                    new AccountRoot { Accounts = new List<HdAccount>(), CoinType = CoinType.Testnet },
                    new AccountRoot { Accounts = new List<HdAccount>(), CoinType = CoinType.Stratis} },
                WalletFilePath = walletFilePath,

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

        private IEnumerable<string> GetWalletFilesPaths()
        {
            // TODO look in user-chosen folder as well.
            // maybe the api can maintain a list of wallet paths it knows about
            var defaultFolderPath = GetDefaultWalletFolderPath();

            // create the directory if it doesn't exist
            Directory.CreateDirectory(defaultFolderPath);
            return Directory.EnumerateFiles(defaultFolderPath, "*.json", SearchOption.TopDirectoryOnly);
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
        /// Gets the path of the default folder in which the wallets will be stored.
        /// </summary>
        /// <returns>The folder path for Windows, Linux or OSX systems.</returns>
        public static string GetDefaultWalletFolderPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return $@"{Environment.GetEnvironmentVariable("AppData")}\Breeze";
            }

            return $"{Environment.GetEnvironmentVariable("HOME")}/.breeze";
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