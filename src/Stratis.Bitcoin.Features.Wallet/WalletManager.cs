using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BuilderExtensions;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Wallet.Tests")]

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// A manager providing operations on wallets.
    /// </summary>
    public class WalletManager : IWalletManager
    {
        /// <summary>Quantity of accounts created in a wallet file when a wallet is restored.</summary>
        private const int WalletRecoveryAccountsCount = 1;

        /// <summary>Quantity of accounts created in a wallet file when a wallet is created.</summary>
        private const int WalletCreationAccountsCount = 1;

        /// <summary>File extension for wallet files.</summary>
        private const string WalletFileExtension = "wallet.json";

        /// <summary>Timer for saving wallet files to the file system.</summary>
        private const int WalletSavetimeIntervalInMinutes = 5;

        /// <summary>
        /// A lock object that protects access to the <see cref="Wallet"/>.
        /// Any of the collections inside Wallet must be synchronized using this lock.
        /// </summary>
        protected readonly object lockObject;

        /// <summary>The async loop we need to wait upon before we can shut down this manager.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>Gets the list of wallets.</summary>
        public ConcurrentBag<Wallet> Wallets { get; }

        /// <summary>The type of coin used in this manager.</summary>
        protected readonly CoinType coinType;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        protected readonly Network network;

        /// <summary>The chain of headers.</summary>
        protected readonly ConcurrentChain chain;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>An object capable of storing <see cref="Wallet"/>s to the file system.</summary>
        private readonly FileStorage<Wallet> fileStorage;

        /// <summary>The broadcast manager.</summary>
        private readonly IBroadcasterManager broadcasterManager;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>The settings for the wallet feature.</summary>
        private readonly WalletSettings walletSettings;

        /// <summary>The settings for the wallet feature.</summary>
        private readonly IScriptAddressReader scriptAddressReader;

        public uint256 WalletTipHash { get; set; }

        // In order to allow faster look-ups of transactions affecting the wallets' addresses,
        // we keep a couple of objects in memory:
        // 1. the list of unspent outputs for checking whether inputs from a transaction are being spent by our wallet and
        // 2. the list of addresses contained in our wallet for checking whether a transaction is being paid to the wallet.
        private Dictionary<OutPoint, TransactionData> outpointLookup;
        internal ScriptToAddressLookup scriptToAddressLookup;

        public WalletManager(
            ILoggerFactory loggerFactory,
            Network network,
            ConcurrentChain chain,
            WalletSettings walletSettings,
            DataFolder dataFolder,
            IWalletFeePolicy walletFeePolicy,
            IAsyncLoopFactory asyncLoopFactory,
            INodeLifetime nodeLifetime,
            IDateTimeProvider dateTimeProvider,
            IScriptAddressReader scriptAddressReader,
            IBroadcasterManager broadcasterManager = null) // no need to know about transactions the node will broadcast to.
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(walletSettings, nameof(walletSettings));
            Guard.NotNull(dataFolder, nameof(dataFolder));
            Guard.NotNull(walletFeePolicy, nameof(walletFeePolicy));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(scriptAddressReader, nameof(scriptAddressReader));

            this.walletSettings = walletSettings;
            this.lockObject = new object();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Wallets = new ConcurrentBag<Wallet>();

            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chain = chain;
            this.asyncLoopFactory = asyncLoopFactory;
            this.nodeLifetime = nodeLifetime;
            this.fileStorage = new FileStorage<Wallet>(dataFolder.WalletPath);
            this.broadcasterManager = broadcasterManager;
            this.scriptAddressReader = scriptAddressReader;
            this.dateTimeProvider = dateTimeProvider;

            // register events
            if (this.broadcasterManager != null)
            {
                this.broadcasterManager.TransactionStateChanged += this.BroadcasterManager_TransactionStateChanged;
            }

            this.scriptToAddressLookup = this.CreateAddressFromScriptLookup();
            this.outpointLookup = new Dictionary<OutPoint, TransactionData>();
        }

        /// <summary>
        /// Creates the <see cref="ScriptToAddressLookup"/> object to use.
        /// </summary>
        /// <remarks>
        /// Override this method and the <see cref="ScriptToAddressLookup"/> object to provide a custom keys lookup.
        /// </remarks>
        /// <returns>A new <see cref="ScriptToAddressLookup"/> object for use by this class.</returns>
        protected virtual ScriptToAddressLookup CreateAddressFromScriptLookup()
        {
            return new ScriptToAddressLookup();
        }

        /// <inheritdoc />
        public virtual Dictionary<string, ScriptTemplate> GetValidStakingTemplates()
        {
            return new Dictionary<string, ScriptTemplate> {
                { "P2PK", PayToPubkeyTemplate.Instance },
                { "P2PKH", PayToPubkeyHashTemplate.Instance } };
        }

        // <inheritdoc />
        public virtual IEnumerable<BuilderExtension> GetTransactionBuilderExtensionsForStaking()
        {
            return new List<BuilderExtension>();
        }

        private void BroadcasterManager_TransactionStateChanged(object sender, TransactionBroadcastEntry transactionEntry)
        {
            if (string.IsNullOrEmpty(transactionEntry.ErrorMessage))
            {
                this.ProcessTransaction(transactionEntry.Transaction, null, null, transactionEntry.State == State.Propagated);
            }
            else
            {
                this.logger.LogTrace("Exception occurred: {0}", transactionEntry.ErrorMessage);
                this.logger.LogTrace("(-)[EXCEPTION]");
            }
        }

        public void Start()
        {
            // Find wallets and load them in memory.
            IEnumerable<Wallet> wallets = this.fileStorage.LoadByFileExtension(WalletFileExtension);

            foreach (Wallet wallet in wallets)
            {
                this.Wallets.Add(wallet);
                foreach (HdAccount account in wallet.GetAccountsByCoinType(this.coinType))
                {
                    this.AddAddressesToMaintainBuffer(account, false);
                    this.AddAddressesToMaintainBuffer(account, true);
                }
            }

            // Load data in memory for faster lookups.
            this.LoadKeysLookupLock();

            // Find the last chain block received by the wallet manager.
            this.WalletTipHash = this.LastReceivedBlockHash();

            // Save the wallets file every 5 minutes to help against crashes.
            this.asyncLoop = this.asyncLoopFactory.Run("Wallet persist job", token =>
            {
                this.SaveWallets();
                this.logger.LogInformation("Wallets saved to file at {0}.", this.dateTimeProvider.GetUtcNow());

                this.logger.LogTrace("(-)[IN_ASYNC_LOOP]");
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
                this.broadcasterManager.TransactionStateChanged -= this.BroadcasterManager_TransactionStateChanged;

            this.asyncLoop?.Dispose();
            this.SaveWallets();
        }

        /// <inheritdoc />
        public Mnemonic CreateWallet(string password, string name, string passphrase, Mnemonic mnemonic = null)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));
            Guard.NotNull(passphrase, nameof(passphrase));

            // Generate the root seed used to generate keys from a mnemonic picked at random
            // and a passphrase optionally provided by the user.
            mnemonic = mnemonic ?? new Mnemonic(Wordlist.English, WordCount.Twelve);

            ExtKey extendedKey = HdOperations.GetExtendedKey(mnemonic, passphrase);

            // Create a wallet file.
            string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.network).ToWif();
            Wallet wallet = this.GenerateWalletFile(name, encryptedSeed, extendedKey.ChainCode);

            // Generate multiple accounts and addresses from the get-go.
            for (int i = 0; i < WalletCreationAccountsCount; i++)
            {
                HdAccount account = wallet.AddNewAccount(password, this.coinType, this.dateTimeProvider.GetTimeOffset());
                IEnumerable<HdAddress> newReceivingAddresses = account.CreateAddresses(this.network, this.walletSettings.UnusedAddressesBuffer);
                IEnumerable<HdAddress> newChangeAddresses = account.CreateAddresses(this.network, this.walletSettings.UnusedAddressesBuffer, true);
                this.UpdateKeysLookupLocked(newReceivingAddresses.Concat(newChangeAddresses));
            }

            // If the chain is downloaded, we set the height of the newly created wallet to it.
            // However, if the chain is still downloading when the user creates a wallet,
            // we wait until it is downloaded in order to set it. Otherwise, the height of the wallet will be the height of the chain at that moment.
            if (this.chain.IsDownloaded())
            {
                this.UpdateLastBlockSyncedHeight(wallet, this.chain.Tip);
            }
            else
            {
                this.UpdateWhenChainDownloaded(new[] { wallet }, this.dateTimeProvider.GetUtcNow());
            }

            // Save the changes to the file and add addresses to be tracked.
            this.SaveWallet(wallet);
            this.Load(wallet);

            return mnemonic;
        }

        /// <inheritdoc />
        public Wallet LoadWallet(string password, string name)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));

            // Load the file from the local system.
            Wallet wallet = this.fileStorage.LoadByFileName($"{name}.{WalletFileExtension}");

            // Check the password.
            try
            {
                if (!wallet.IsExtPubKeyWallet)
                    Key.Parse(wallet.EncryptedSeed, password, wallet.Network);
            }
            catch (Exception ex)
            {
                this.logger.LogTrace("Exception occurred: {0}", ex.ToString());
                this.logger.LogTrace("(-)[EXCEPTION]");
                throw new SecurityException(ex.Message);
            }

            this.Load(wallet);

            return wallet;
        }

        /// <inheritdoc />
        public Wallet RecoverWallet(string password, string name, string mnemonic, DateTime creationTime, string passphrase)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));
            Guard.NotEmpty(mnemonic, nameof(mnemonic));
            Guard.NotNull(passphrase, nameof(passphrase));

            // Generate the root seed used to generate keys.
            ExtKey extendedKey;
            try
            {
                extendedKey = HdOperations.GetExtendedKey(mnemonic, passphrase);
            }
            catch (NotSupportedException ex)
            {
                this.logger.LogTrace("Exception occurred: {0}", ex.ToString());
                this.logger.LogTrace("(-)[EXCEPTION]");

                if (ex.Message == "Unknown")
                    throw new WalletException("Please make sure you enter valid mnemonic words.");

                throw;
            }

            // Create a wallet file.
            string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.network).ToWif();
            Wallet wallet = this.GenerateWalletFile(name, encryptedSeed, extendedKey.ChainCode, creationTime);

            // Generate multiple accounts and addresses from the get-go.
            for (int i = 0; i < WalletRecoveryAccountsCount; i++)
            {
                HdAccount account;
                lock (this.lockObject)
                {
                    account = wallet.AddNewAccount(password, this.coinType, this.dateTimeProvider.GetTimeOffset());
                }

                IEnumerable<HdAddress> newReceivingAddresses = account.CreateAddresses(this.network, this.walletSettings.UnusedAddressesBuffer);
                IEnumerable<HdAddress> newChangeAddresses = account.CreateAddresses(this.network, this.walletSettings.UnusedAddressesBuffer, true);
                this.UpdateKeysLookupLocked(newReceivingAddresses.Concat(newChangeAddresses));
            }

            // If the chain is downloaded, we set the height of the recovered wallet to that of the recovery date.
            // However, if the chain is still downloading when the user restores a wallet,
            // we wait until it is downloaded in order to set it. Otherwise, the height of the wallet may not be known.
            if (this.chain.IsDownloaded())
            {
                int blockSyncStart = this.chain.GetHeightAtTime(creationTime);
                this.UpdateLastBlockSyncedHeight(wallet, this.chain.GetBlock(blockSyncStart));
            }
            else
            {
                this.UpdateWhenChainDownloaded(new[] { wallet }, creationTime);
            }

            this.SaveWallet(wallet);
            this.Load(wallet);

            return wallet;
        }

        /// <inheritdoc />
        public Wallet RecoverWallet(string name, ExtPubKey extPubKey, int accountIndex, DateTime creationTime)
        {
            Guard.NotEmpty(name, nameof(name));
            Guard.NotNull(extPubKey, nameof(extPubKey));
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:'{5}')", nameof(name), name, nameof(extPubKey), extPubKey, nameof(accountIndex), accountIndex);

            // Create a wallet file.
            Wallet wallet = this.GenerateExtPubKeyOnlyWalletFile(name, creationTime);

            // Generate account
            HdAccount account;
            lock (this.lockObject)
            {
                account = wallet.AddNewAccount(this.coinType, extPubKey, accountIndex, this.dateTimeProvider.GetTimeOffset());
            }

            IEnumerable<HdAddress> newReceivingAddresses = account.CreateAddresses(this.network, this.walletSettings.UnusedAddressesBuffer);
            IEnumerable<HdAddress> newChangeAddresses = account.CreateAddresses(this.network, this.walletSettings.UnusedAddressesBuffer, true);
            this.UpdateKeysLookupLocked(newReceivingAddresses.Concat(newChangeAddresses));

            // If the chain is downloaded, we set the height of the recovered wallet to that of the recovery date.
            // However, if the chain is still downloading when the user restores a wallet,
            // we wait until it is downloaded in order to set it. Otherwise, the height of the wallet may not be known.
            if (this.chain.IsDownloaded())
            {
                int blockSyncStart = this.chain.GetHeightAtTime(creationTime);
                this.UpdateLastBlockSyncedHeight(wallet, this.chain.GetBlock(blockSyncStart));
            }
            else
            {
                this.UpdateWhenChainDownloaded(new[] { wallet }, creationTime);
            }

            // Save the changes to the file and add addresses to be tracked.
            this.SaveWallet(wallet);
            this.Load(wallet);
            return wallet;
        }

        /// <inheritdoc />
        public HdAccount GetUnusedAccount(string walletName, string password)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotEmpty(password, nameof(password));

            Wallet wallet = this.GetWalletByName(walletName);

            if (wallet.IsExtPubKeyWallet)
            {
                this.logger.LogTrace("(-)[CANNOT_ADD_ACCOUNT_TO_EXTPUBKEY_WALLET]");
                throw new CannotAddAccountToXpubKeyWalletException("Use recover-via-extpubkey instead.");
            }

            HdAccount res = this.GetUnusedAccount(wallet, password);
            return res;
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
                    this.logger.LogTrace("(-)[ACCOUNT_FOUND]");
                    return account;
                }

                // No unused account was found, create a new one.
                account = wallet.AddNewAccount(password, this.coinType, this.dateTimeProvider.GetTimeOffset());
                IEnumerable<HdAddress> newReceivingAddresses = account.CreateAddresses(this.network, this.walletSettings.UnusedAddressesBuffer);
                IEnumerable<HdAddress> newChangeAddresses = account.CreateAddresses(this.network, this.walletSettings.UnusedAddressesBuffer, true);
                this.UpdateKeysLookupLocked(newReceivingAddresses.Concat(newChangeAddresses));
            }

            // Save the changes to the file.
            this.SaveWallet(wallet);

            return account;
        }

        public string GetExtPubKey(WalletAccountReference accountReference)
        {
            Guard.NotNull(accountReference, nameof(accountReference));

            Wallet wallet = this.GetWalletByName(accountReference.WalletName);

            string extPubKey;
            lock (this.lockObject)
            {
                // Get the account.
                HdAccount account = wallet.GetAccountByCoinType(accountReference.AccountName, this.coinType);
                extPubKey = account.ExtendedPubKey;
            }

            return extPubKey;
        }

        /// <inheritdoc />
        public HdAddress GetUnusedAddress(WalletAccountReference accountReference)
        {
            HdAddress res = this.GetUnusedAddresses(accountReference, 1).Single();

            return res;
        }

        /// <inheritdoc />
        public HdAddress GetUnusedChangeAddress(WalletAccountReference accountReference)
        {
            HdAddress res = this.GetUnusedAddresses(accountReference, 1, true).Single();

            return res;
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isChange = false)
        {
            Guard.NotNull(accountReference, nameof(accountReference));
            Guard.Assert(count > 0);

            Wallet wallet = this.GetWalletByName(accountReference.WalletName);

            bool generated = false;
            IEnumerable<HdAddress> addresses;

            lock (this.lockObject)
            {
                // Get the account.
                HdAccount account = wallet.GetAccountByCoinType(accountReference.AccountName, this.coinType);

                List<HdAddress> unusedAddresses = isChange ?
                    account.InternalAddresses.Where(acc => !acc.Transactions.Any()).ToList() :
                    account.ExternalAddresses.Where(acc => !acc.Transactions.Any()).ToList();

                int diff = unusedAddresses.Count - count;
                var newAddresses = new List<HdAddress>();
                if (diff < 0)
                {
                    newAddresses = account.CreateAddresses(this.network, Math.Abs(diff), isChange: isChange).ToList();
                    this.UpdateKeysLookupLocked(newAddresses);
                    generated = true;
                }

                addresses = unusedAddresses.Concat(newAddresses).OrderBy(x => x.Index).Take(count);
            }

            if (generated)
            {
                // Save the changes to the file.
                this.SaveWallet(wallet);
            }

            return addresses;
        }

        /// <inheritdoc />
        public (string folderPath, IEnumerable<string>) GetWalletsFiles()
        {
            return (this.fileStorage.FolderPath, this.fileStorage.GetFilesNames(this.GetWalletFileExtension()));
        }

        /// <inheritdoc />
        public IEnumerable<AccountHistory> GetHistory(string walletName, string accountName = null)
        {
            Guard.NotEmpty(walletName, nameof(walletName));

            // In order to calculate the fee properly we need to retrieve all the transactions with spending details.
            Wallet wallet = this.GetWalletByName(walletName);

            var accountsHistory = new List<AccountHistory>();

            lock (this.lockObject)
            {
                var accounts = new List<HdAccount>();
                if (!string.IsNullOrEmpty(accountName))
                {
                    accounts.Add(wallet.GetAccountByCoinType(accountName, this.coinType));
                }
                else
                {
                    accounts.AddRange(wallet.GetAccountsByCoinType(this.coinType));
                }

                foreach (HdAccount account in accounts)
                {
                    accountsHistory.Add(this.GetHistory(account));
                }
            }

            return accountsHistory;
        }

        /// <inheritdoc />
        public AccountHistory GetHistory(HdAccount account)
        {
            Guard.NotNull(account, nameof(account));
            FlatHistory[] items;
            lock (this.lockObject)
            {
                // Get transactions contained in the account.
                items = account.GetCombinedAddresses()
                    .Where(a => a.Transactions.Any())
                    .SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToArray();
            }

            return new AccountHistory { Account = account, History = items };
        }

        /// <inheritdoc />
        public IEnumerable<AccountBalance> GetBalances(string walletName, string accountName = null)
        {
            var balances = new List<AccountBalance>();

            lock (this.lockObject)
            {
                Wallet wallet = this.GetWalletByName(walletName);

                var accounts = new List<HdAccount>();
                if (!string.IsNullOrEmpty(accountName))
                {
                    accounts.Add(wallet.GetAccountByCoinType(accountName, this.coinType));
                }
                else
                {
                    accounts.AddRange(wallet.GetAccountsByCoinType(this.coinType));
                }

                foreach (HdAccount account in accounts)
                {
                    (Money amountConfirmed, Money amountUnconfirmed) result = account.GetSpendableAmount();

                    balances.Add(new AccountBalance
                    {
                        Account = account,
                        AmountConfirmed = result.amountConfirmed,
                        AmountUnconfirmed = result.amountUnconfirmed
                    });
                }
            }

            return balances;
        }

        /// <inheritdoc />
        public AddressBalance GetAddressBalance(string address)
        {
            Guard.NotEmpty(address, nameof(address));

            var balance = new AddressBalance
            {
                Address = address,
                CoinType = this.coinType
            };

            lock (this.lockObject)
            {
                HdAddress hdAddress = null;

                foreach (Wallet wallet in this.Wallets)
                {
                    hdAddress = wallet.GetAllAddressesByCoinType(this.coinType).FirstOrDefault(a => a.Address == address);
                    if (hdAddress == null) continue;

                    (Money amountConfirmed, Money amountUnconfirmed) result = hdAddress.GetSpendableAmount();

                    balance.AmountConfirmed = result.amountConfirmed;
                    balance.AmountUnconfirmed = result.amountUnconfirmed;

                    break;
                }

                if (hdAddress == null)
                {
                    this.logger.LogTrace("(-)[ADDRESS_NOT_FOUND]");
                    throw new WalletException($"Address '{address}' not found in wallets.");
                }
            }

            return balance;
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

            HdAccount[] res = null;
            lock (this.lockObject)
            {
                res = wallet.GetAccountsByCoinType(this.coinType).ToArray();
            }
            return res;
        }

        /// <inheritdoc />
        public int LastBlockHeight()
        {
            if (!this.Wallets.Any())
            {
                int height = this.chain.Tip.Height;
                this.logger.LogTrace("(-)[NO_WALLET]:{0}", height);
                return height;
            }

            int res;
            lock (this.lockObject)
            {
                res = this.Wallets.Min(w => w.AccountsRoot.SingleOrDefault(a => a.CoinType == this.coinType)?.LastBlockSyncedHeight) ?? 0;
            }

            return res;
        }

        /// <inheritdoc />
        public bool ContainsWallets => this.Wallets.Any();

        /// <summary>
        /// Gets the hash of the last block received by the wallets.
        /// </summary>
        /// <returns>Hash of the last block received by the wallets.</returns>
        public uint256 LastReceivedBlockHash()
        {
            if (!this.Wallets.Any())
            {
                uint256 hash = this.chain.Tip.HashBlock;
                this.logger.LogTrace("(-)[NO_WALLET]:'{0}'", hash);
                return hash;
            }

            uint256 lastBlockSyncedHash;
            lock (this.lockObject)
            {
                lastBlockSyncedHash = this.Wallets
                    .Select(w => w.AccountsRoot.SingleOrDefault(a => a.CoinType == this.coinType))
                    .Where(w => w != null)
                    .OrderBy(o => o.LastBlockSyncedHeight)
                    .FirstOrDefault()?.LastBlockSyncedHash;

                // If details about the last block synced are not present in the wallet,
                // find out which is the oldest wallet and set the last block synced to be the one at this date.
                if (lastBlockSyncedHash == null)
                {
                    this.logger.LogWarning("There were no details about the last block synced in the wallets.");
                    DateTimeOffset earliestWalletDate = this.Wallets.Min(c => c.CreationTime);
                    this.UpdateWhenChainDownloaded(this.Wallets, earliestWalletDate.DateTime);
                    lastBlockSyncedHash = this.chain.Tip.HashBlock;
                }
            }

            return lastBlockSyncedHash;
        }

        /// <inheritdoc />
        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWallet(string walletName, int confirmations = 0)
        {
            return this.GetSpendableTransactionsInWallet(walletName, confirmations, Wallet.NormalAccounts);
        }

        public virtual IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWalletForStaking(string walletName, int confirmations = 0)
        {
            return this.GetSpendableTransactionsInWallet(walletName, confirmations);
        }

        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWallet(string walletName, int confirmations, Func<HdAccount, bool> accountFilter)
        {
            Guard.NotEmpty(walletName, nameof(walletName));

            Wallet wallet = this.GetWalletByName(walletName);
            UnspentOutputReference[] res = null;
            lock (this.lockObject)
            {
                res = wallet.GetAllSpendableTransactions(this.coinType, this.chain.Tip.Height, confirmations, accountFilter).ToArray();
            }
            
            return res;
        }

        /// <inheritdoc />
        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference walletAccountReference, int confirmations = 0)
        {
            Guard.NotNull(walletAccountReference, nameof(walletAccountReference));

            Wallet wallet = this.GetWalletByName(walletAccountReference.WalletName);
            UnspentOutputReference[] res = null;
            lock (this.lockObject)
            {
                HdAccount account = wallet.GetAccountByCoinType(walletAccountReference.AccountName, this.coinType);

                if (account == null)
                {
                    this.logger.LogTrace("(-)[ACT_NOT_FOUND]");
                    throw new WalletException(
                        $"Account '{walletAccountReference.AccountName}' in wallet '{walletAccountReference.WalletName}' not found.");
                }

                res = account.GetSpendableTransactions(this.chain.Tip.Height, this.network, confirmations).ToArray();
            }
            
            return res;
        }

        /// <inheritdoc />
        public void RemoveBlocks(ChainedHeader fork)
        {
            Guard.NotNull(fork, nameof(fork));

            lock (this.lockObject)
            {
                IEnumerable<HdAddress> allAddresses = this.scriptToAddressLookup.Values;
                foreach (HdAddress address in allAddresses)
                {
                    // Remove all the UTXO that have been reorged.
                    IEnumerable<TransactionData> makeUnspendable = address.Transactions.Where(w => w.BlockHeight > fork.Height).ToList();
                    foreach (TransactionData transactionData in makeUnspendable)
                        address.Transactions.Remove(transactionData);

                    // Bring back all the UTXO that are now spendable after the reorg.
                    IEnumerable<TransactionData> makeSpendable = address.Transactions.Where(w => (w.SpendingDetails != null) && (w.SpendingDetails.BlockHeight > fork.Height));
                    foreach (TransactionData transactionData in makeSpendable)
                        transactionData.SpendingDetails = null;
                }

                this.UpdateLastBlockSyncedHeight(fork);
            }
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block, ChainedHeader chainedHeader)
        {
            Guard.NotNull(block, nameof(block));
            Guard.NotNull(chainedHeader, nameof(chainedHeader));

            // If there is no wallet yet, update the wallet tip hash and do nothing else.
            if (!this.Wallets.Any())
            {
                this.WalletTipHash = chainedHeader.HashBlock;
                this.logger.LogTrace("(-)[NO_WALLET]");
                return;
            }

            // Is this the next block.
            if (chainedHeader.Header.HashPrevBlock != this.WalletTipHash)
            {
                this.logger.LogTrace("New block's previous hash '{0}' does not match current wallet's tip hash '{1}'.", chainedHeader.Header.HashPrevBlock, this.WalletTipHash);

                // Are we still on the main chain.
                ChainedHeader current = this.chain.GetBlock(this.WalletTipHash);
                if (current == null)
                {
                    this.logger.LogTrace("(-)[REORG]");
                    throw new WalletException("Reorg");
                }

                // The block coming in to the wallet should never be ahead of the wallet.
                // If the block is behind, let it pass.
                if (chainedHeader.Height > current.Height)
                {
                    this.logger.LogTrace("(-)[BLOCK_TOO_FAR]");
                    throw new WalletException("block too far in the future has arrived to the wallet");
                }
            }

            lock (this.lockObject)
            {
                bool trxFoundInBlock = false;
                foreach (Transaction transaction in block.Transactions)
                {
                    bool trxFound = this.ProcessTransaction(transaction, chainedHeader.Height, block, true);
                    if (trxFound)
                    {
                        trxFoundInBlock = true;
                    }
                }

                // Update the wallets with the last processed block height.
                // It's important that updating the height happens after the block processing is complete,
                // as if the node is stopped, on re-opening it will start updating from the previous height.
                this.UpdateLastBlockSyncedHeight(chainedHeader);

                if (trxFoundInBlock)
                {
                    this.logger.LogDebug("Block {0} contains at least one transaction affecting the user's wallet(s).", chainedHeader);
                }
            }
        }

        /// <inheritdoc />
        public bool ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null, bool isPropagated = true)
        {
            Guard.NotNull(transaction, nameof(transaction));
            uint256 hash = transaction.GetHash();

            bool foundReceivingTrx = false, foundSendingTrx = false;

            lock (this.lockObject)
            {
                // Check the outputs, ignoring the ones with a 0 amount.
                foreach (TxOut utxo in transaction.Outputs.Where(o => o.Value != Money.Zero))
                {
                    // Check if the outputs contain one of our addresses.
                    if (this.scriptToAddressLookup.TryGetValue(utxo.ScriptPubKey, out HdAddress _))
                    {
                        this.AddTransactionToWallet(transaction, utxo, blockHeight, block, isPropagated);
                        foundReceivingTrx = true;
                        this.logger.LogDebug("Transaction '{0}' contained funds received by the user's wallet(s).", hash);
                    }
                }

                // Check the inputs - include those that have a reference to a transaction containing one of our scripts and the same index.
                foreach (TxIn input in transaction.Inputs)
                {
                    if (!this.outpointLookup.TryGetValue(input.PrevOut, out TransactionData tTx))
                    {
                        continue;
                    }

                    // Get the details of the outputs paid out.
                    IEnumerable<TxOut> paidOutTo = transaction.Outputs.Where(o =>
                    {
                        // If script is empty ignore it.
                        if (o.IsEmpty)
                            return false;

                        // Check if the destination script is one of the wallet's.
                        bool found = this.scriptToAddressLookup.TryGetValue(o.ScriptPubKey, out HdAddress addr);

                        // Include the keys not included in our wallets (external payees).
                        if (!found)
                            return true;

                        // Include the keys that are in the wallet but that are for receiving
                        // addresses (which would mean the user paid itself).
                        // We also exclude the keys involved in a staking transaction.
                        return !addr.IsChangeAddress() && !transaction.IsCoinStake;
                    });

                    this.AddSpendingTransactionToWallet(transaction, paidOutTo, tTx.Id, tTx.Index, blockHeight, block);
                    foundSendingTrx = true;
                    this.logger.LogDebug("Transaction '{0}' contained funds sent by the user's wallet(s).", hash);
                }
            }

            return foundSendingTrx || foundReceivingTrx;
        }

        /// <summary>
        /// Adds a transaction that credits the wallet with new coins.
        /// This method is can be called many times for the same transaction (idempotent).
        /// </summary>
        /// <param name="transaction">The transaction from which details are added.</param>
        /// <param name="utxo">The unspent output to add to the wallet.</param>
        /// <param name="blockHeight">Height of the block.</param>
        /// <param name="block">The block containing the transaction to add.</param>
        /// <param name="isPropagated">Propagation state of the transaction.</param>
        private void AddTransactionToWallet(Transaction transaction, TxOut utxo, int? blockHeight = null, Block block = null, bool isPropagated = true)
        {
            Guard.NotNull(transaction, nameof(transaction));
            Guard.NotNull(utxo, nameof(utxo));

            uint256 transactionHash = transaction.GetHash();

            // Get the collection of transactions to add to.
            Script script = utxo.ScriptPubKey;
            this.scriptToAddressLookup.TryGetValue(script, out HdAddress address);
            ICollection<TransactionData> addressTransactions = address.Transactions;

            // Check if a similar UTXO exists or not (same transaction ID and same index).
            // New UTXOs are added, existing ones are updated.
            int index = transaction.Outputs.IndexOf(utxo);
            Money amount = utxo.Value;
            TransactionData foundTransaction = addressTransactions.FirstOrDefault(t => (t.Id == transactionHash) && (t.Index == index));
            if (foundTransaction == null)
            {
                this.logger.LogTrace("UTXO '{0}-{1}' not found, creating.", transactionHash, index);
                var newTransaction = new TransactionData
                {
                    Amount = amount,
                    IsCoinBase = transaction.IsCoinBase == false ? (bool?)null : true,
                    IsCoinStake = transaction.IsCoinStake == false ? (bool?)null : true,
                    BlockHeight = blockHeight,
                    BlockHash = block?.GetHash(),
                    Id = transactionHash,
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? transaction.Time),
                    Index = index,
                    ScriptPubKey = script,
                    Hex = this.walletSettings.SaveTransactionHex ? transaction.ToHex() : null,
                    IsPropagated = isPropagated
                };

                // Add the Merkle proof to the (non-spending) transaction.
                if (block != null)
                {
                    newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                }

                addressTransactions.Add(newTransaction);
                this.AddInputKeysLookupLocked(newTransaction);
            }
            else
            {
                this.logger.LogTrace("Transaction ID '{0}' found, updating.", transactionHash);

                // Update the block height and block hash.
                if ((foundTransaction.BlockHeight == null) && (blockHeight != null))
                {
                    foundTransaction.BlockHeight = blockHeight;
                    foundTransaction.BlockHash = block?.GetHash();
                }

                // Update the block time.
                if (block != null)
                {
                    foundTransaction.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
                }

                // Add the Merkle proof now that the transaction is confirmed in a block.
                if ((block != null) && (foundTransaction.MerkleProof == null))
                {
                    foundTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                }

                if (isPropagated)
                    foundTransaction.IsPropagated = true;
            }

            this.TransactionFoundInternal(script);
        }

        /// <summary>
        /// Mark an output as spent, the credit of the output will not be used to calculate the balance.
        /// The output will remain in the wallet for history (and reorg).
        /// </summary>
        /// <param name="transaction">The transaction from which details are added.</param>
        /// <param name="paidToOutputs">A list of payments made out</param>
        /// <param name="spendingTransactionId">The id of the transaction containing the output being spent, if this is a spending transaction.</param>
        /// <param name="spendingTransactionIndex">The index of the output in the transaction being referenced, if this is a spending transaction.</param>
        /// <param name="blockHeight">Height of the block.</param>
        /// <param name="block">The block containing the transaction to add.</param>
        private void AddSpendingTransactionToWallet(Transaction transaction, IEnumerable<TxOut> paidToOutputs,
            uint256 spendingTransactionId, int? spendingTransactionIndex, int? blockHeight = null, Block block = null)
        {
            Guard.NotNull(transaction, nameof(transaction));
            Guard.NotNull(paidToOutputs, nameof(paidToOutputs));

            // Get the transaction being spent.
            TransactionData spentTransaction = this.scriptToAddressLookup.Values.Distinct().SelectMany(v => v.Transactions)
                .SingleOrDefault(t => (t.Id == spendingTransactionId) && (t.Index == spendingTransactionIndex));
            if (spentTransaction == null)
            {
                // Strange, why would it be null?
                this.logger.LogTrace("(-)[TX_NULL]");
                return;
            }

            // If the details of this spending transaction are seen for the first time.
            if (spentTransaction.SpendingDetails == null)
            {
                this.logger.LogTrace("Spending UTXO '{0}-{1}' is new.", spendingTransactionId, spendingTransactionIndex);

                var payments = new List<PaymentDetails>();
                foreach (TxOut paidToOutput in paidToOutputs)
                {
                    // Figure out how to retrieve the destination address.
                    string destinationAddress = this.scriptAddressReader.GetAddressFromScriptPubKey(this.network, paidToOutput.ScriptPubKey);
                    if (destinationAddress == string.Empty)
                        if (this.scriptToAddressLookup.TryGetValue(paidToOutput.ScriptPubKey, out HdAddress destination))
                            destinationAddress = destination.Address;

                    payments.Add(new PaymentDetails
                    {
                        DestinationScriptPubKey = paidToOutput.ScriptPubKey,
                        DestinationAddress = destinationAddress,
                        Amount = paidToOutput.Value
                    });
                }

                var spendingDetails = new SpendingDetails
                {
                    TransactionId = transaction.GetHash(),
                    Payments = payments,
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? transaction.Time),
                    BlockHeight = blockHeight,
                    Hex = this.walletSettings.SaveTransactionHex ? transaction.ToHex() : null,
                    IsCoinStake = transaction.IsCoinStake == false ? (bool?)null : true
                };

                spentTransaction.SpendingDetails = spendingDetails;
                spentTransaction.MerkleProof = null;
            }
            else // If this spending transaction is being confirmed in a block.
            {
                this.logger.LogTrace("Spending transaction ID '{0}' is being confirmed, updating.", spendingTransactionId);

                // Update the block height.
                if (spentTransaction.SpendingDetails.BlockHeight == null && blockHeight != null)
                {
                    spentTransaction.SpendingDetails.BlockHeight = blockHeight;
                }

                // Update the block time to be that of the block in which the transaction is confirmed.
                if (block != null)
                {
                    spentTransaction.SpendingDetails.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
                }
            }

            // If the transaction is spent and confirmed, we remove the UTXO from the lookup dictionary.
            if (spentTransaction.BlockHeight != null)
            {
                this.RemoveInputKeysLookupLock(spentTransaction);
            }
        }

        public virtual void TransactionFoundInternal(Script script, Func<HdAccount, bool> accountFilter = null)
        {
            foreach (Wallet wallet in this.Wallets)
            {
                foreach (HdAccount account in wallet.GetAccountsByCoinType(this.coinType, accountFilter ?? Wallet.NormalAccounts))
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

                    IEnumerable<HdAddress> newAddresses = this.AddAddressesToMaintainBuffer(account, isChange);

                    this.UpdateKeysLookupLocked(newAddresses);
                }
            }
        }

        private IEnumerable<HdAddress> AddAddressesToMaintainBuffer(HdAccount account, bool isChange)
        {
            HdAddress lastUsedAddress = account.GetLastUsedAddress(isChange);
            int lastUsedAddressIndex = lastUsedAddress?.Index ?? -1;
            int addressesCount = isChange ? account.InternalAddresses.Count() : account.ExternalAddresses.Count();
            int emptyAddressesCount = addressesCount - lastUsedAddressIndex - 1;
            int addressesToAdd = this.walletSettings.UnusedAddressesBuffer - emptyAddressesCount;

            return addressesToAdd > 0 ? account.CreateAddresses(this.network, addressesToAdd, isChange) : new List<HdAddress>();
        }

        /// <inheritdoc />
        public void DeleteWallet()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void SaveWallets()
        {
            foreach (Wallet wallet in this.Wallets)
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
        public void UpdateLastBlockSyncedHeight(ChainedHeader chainedHeader)
        {
            Guard.NotNull(chainedHeader, nameof(chainedHeader));

            // Update the wallets with the last processed block height.
            foreach (Wallet wallet in this.Wallets)
            {
                this.UpdateLastBlockSyncedHeight(wallet, chainedHeader);
            }

            this.WalletTipHash = chainedHeader.HashBlock;
        }

        /// <inheritdoc />
        public void UpdateLastBlockSyncedHeight(Wallet wallet, ChainedHeader chainedHeader)
        {
            Guard.NotNull(wallet, nameof(wallet));
            Guard.NotNull(chainedHeader, nameof(chainedHeader));

            // The block locator will help when the wallet
            // needs to rewind this will be used to find the fork.
            wallet.BlockLocator = chainedHeader.GetLocator().Blocks;

            lock (this.lockObject)
            {
                wallet.SetLastBlockDetailsByCoinType(this.coinType, chainedHeader);
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
        /// <exception cref="WalletException">Thrown if wallet cannot be created.</exception>
        private Wallet GenerateWalletFile(string name, string encryptedSeed, byte[] chainCode, DateTimeOffset? creationTime = null)
        {
            Guard.NotEmpty(name, nameof(name));
            Guard.NotEmpty(encryptedSeed, nameof(encryptedSeed));
            Guard.NotNull(chainCode, nameof(chainCode));

            // Check if any wallet file already exists, with case insensitive comparison.
            if (this.Wallets.Any(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                this.logger.LogTrace("(-)[WALLET_ALREADY_EXISTS]");
                throw new WalletException($"Wallet with name '{name}' already exists.");
            }

            List<Wallet> similarWallets = this.Wallets.Where(w => w.EncryptedSeed == encryptedSeed).ToList();
            if (similarWallets.Any())
            {
                this.logger.LogTrace("(-)[SAME_PK_ALREADY_EXISTS]");
                throw new WalletException("Cannot create this wallet as a wallet with the same private key already exists. If you want to restore your wallet from scratch, " +
                                                    $"please remove the file {string.Join(", ", similarWallets.Select(w => w.Name))}.{WalletFileExtension} from '{this.fileStorage.FolderPath}' and try restoring the wallet again. " +
                                                    "Make sure you have your mnemonic and your password handy!");
            }

            var walletFile = new Wallet
            {
                Name = name,
                EncryptedSeed = encryptedSeed,
                ChainCode = chainCode,
                CreationTime = creationTime ?? this.dateTimeProvider.GetTimeOffset(),
                Network = this.network,
                AccountsRoot = new List<AccountRoot> { new AccountRoot() { Accounts = new List<HdAccount>(), CoinType = this.coinType } },
            };

            // Create a folder if none exists and persist the file.
            this.SaveWallet(walletFile);
            
            return walletFile;
        }

        /// <summary>
        /// Generates the wallet file without private key and chain code.
        /// For use with only the extended public key.
        /// </summary>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="creationTime">The time this wallet was created.</param>
        /// <returns>The wallet object that was saved into the file system.</returns>
        /// <exception cref="WalletException">Thrown if wallet cannot be created.</exception>
        private Wallet GenerateExtPubKeyOnlyWalletFile(string name, DateTimeOffset? creationTime = null)
        {
            Guard.NotEmpty(name, nameof(name));

            // Check if any wallet file already exists, with case insensitive comparison.
            if (this.Wallets.Any(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                this.logger.LogTrace("(-)[WALLET_ALREADY_EXISTS]");
                throw new WalletException($"Wallet with name '{name}' already exists.");
            }

            var walletFile = new Wallet
            {
                Name = name,
                IsExtPubKeyWallet = true,
                CreationTime = creationTime ?? this.dateTimeProvider.GetTimeOffset(),
                Network = this.network,
                AccountsRoot = new List<AccountRoot> { new AccountRoot() { Accounts = new List<HdAccount>(), CoinType = this.coinType } },
            };

            // Create a folder if none exists and persist the file.
            this.SaveWallet(walletFile);
            
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
                this.logger.LogTrace("(-)[NOT_FOUND]");
                return;
            }

            this.Wallets.Add(wallet);
        }

        /// <summary>
        /// Loads the keys and transactions we're tracking in memory for faster lookups.
        /// </summary>
        public void LoadKeysLookupLock()
        {
            lock (this.lockObject)
            {
                foreach (Wallet wallet in this.Wallets)
                {
                    IEnumerable<HdAddress> addresses = wallet.GetAllAddressesByCoinType(this.coinType, a => true);
                    foreach (HdAddress address in addresses)
                    {
                        this.scriptToAddressLookup[address.ScriptPubKey] = address;
                        if (address.Pubkey != null)
                            this.scriptToAddressLookup[address.Pubkey] = address;

                        // Get the UTXOs that are unspent or spent but not confirmed.
                        // We only exclude from the list the confirmed spent UTXOs.
                        foreach (TransactionData transaction in address.Transactions.Where(t => t.SpendingDetails?.BlockHeight == null))
                        {
                            this.outpointLookup[new OutPoint(transaction.Id, transaction.Index)] = transaction;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Update the keys and transactions we're tracking in memory for faster lookups.
        /// </summary>
        public void UpdateKeysLookupLocked(IEnumerable<HdAddress> addresses)
        {
            if (addresses == null || !addresses.Any())
            {
                return;
            }

            lock (this.lockObject)
            {
                foreach (HdAddress address in addresses)
                {
                    this.scriptToAddressLookup[address.ScriptPubKey] = address;
                    if (address.Pubkey != null)
                        this.scriptToAddressLookup[address.Pubkey] = address;
                }
            }
        }

        /// <summary>
        /// Add to the list of unspent outputs kept in memory for faster lookups.
        /// </summary>
        private void AddInputKeysLookupLocked(TransactionData transactionData)
        {
            Guard.NotNull(transactionData, nameof(transactionData));

            lock (this.lockObject)
            {
                this.outpointLookup[new OutPoint(transactionData.Id, transactionData.Index)] = transactionData;
            }
        }

        /// <summary>
        /// Remove from the list of unspent outputs kept in memory.
        /// </summary>
        private void RemoveInputKeysLookupLock(TransactionData transactionData)
        {
            Guard.NotNull(transactionData, nameof(transactionData));
            Guard.NotNull(transactionData.SpendingDetails, nameof(transactionData.SpendingDetails));

            lock (this.lockObject)
            {
                this.outpointLookup.Remove(new OutPoint(transactionData.Id, transactionData.Index));
            }
        }

        /// <inheritdoc />
        public IEnumerable<string> GetWalletsNames()
        {
            return this.Wallets.Select(w => w.Name);
        }

        /// <inheritdoc />
        public Wallet GetWalletByName(string walletName)
        {
            Wallet wallet = this.Wallets.SingleOrDefault(w => w.Name == walletName);
            if (wallet == null)
            {
                this.logger.LogTrace("(-)[WALLET_NOT_FOUND]");
                throw new WalletException($"No wallet with name '{walletName}' could be found.");
            }

            return wallet;
        }

        /// <inheritdoc />
        public ICollection<uint256> GetFirstWalletBlockLocator()
        {
            return this.Wallets.First().BlockLocator;
        }

        /// <inheritdoc />
        public int? GetEarliestWalletHeight()
        {
            return this.Wallets.Min(w => w.AccountsRoot.Single(a => a.CoinType == this.coinType).LastBlockSyncedHeight);
        }

        /// <inheritdoc />
        public DateTimeOffset GetOldestWalletCreationTime()
        {
            return this.Wallets.Min(w => w.CreationTime);
        }

        /// <inheritdoc />
        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsByIdsLocked(string walletName, IEnumerable<uint256> transactionsIds)
        {
            Guard.NotNull(transactionsIds, nameof(transactionsIds));
            Guard.NotEmpty(walletName, nameof(walletName));

            List<uint256> idsToRemove = transactionsIds.ToList();
            Wallet wallet = this.GetWallet(walletName);

            var result = new HashSet<(uint256, DateTimeOffset)>();

            lock (this.lockObject)
            {
                IEnumerable<HdAccount> accounts = wallet.GetAccountsByCoinType(this.coinType);
                foreach (HdAccount account in accounts)
                {
                    foreach (HdAddress address in account.GetCombinedAddresses())
                    {
                        for (int i = 0; i < address.Transactions.Count; i++)
                        {
                            TransactionData transaction = address.Transactions.ElementAt(i);

                            // Remove the transaction from the list of transactions affecting an address.
                            // Only transactions that haven't been confirmed in a block can be removed.
                            if (!transaction.IsConfirmed() && idsToRemove.Contains(transaction.Id))
                            {
                                result.Add((transaction.Id, transaction.CreationTime));
                                address.Transactions = address.Transactions.Except(new[] { transaction }).ToList();
                                i--;
                            }

                            // Remove the spending transaction object containing this transaction id.
                            if (transaction.SpendingDetails != null && !transaction.SpendingDetails.IsSpentConfirmed() && idsToRemove.Contains(transaction.SpendingDetails.TransactionId))
                            {
                                result.Add((transaction.SpendingDetails.TransactionId, transaction.SpendingDetails.CreationTime));
                                address.Transactions.ElementAt(i).SpendingDetails = null;
                            }
                        }
                    }
                }
            }

            if (result.Any())
            {
                this.SaveWallet(wallet);
            }

            return result;
        }

        /// <inheritdoc />
        public HashSet<(uint256, DateTimeOffset)> RemoveAllTransactions(string walletName)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Wallet wallet = this.GetWallet(walletName);

            var removedTransactions = new HashSet<(uint256, DateTimeOffset)>();

            lock (this.lockObject)
            {
                IEnumerable<HdAccount> accounts = wallet.GetAccountsByCoinType(this.coinType);
                foreach (HdAccount account in accounts)
                {
                    foreach (HdAddress address in account.GetCombinedAddresses())
                    {
                        removedTransactions.UnionWith(address.Transactions.Select(t => (t.Id, t.CreationTime)));
                        address.Transactions.Clear();
                    }
                }
            }

            if (removedTransactions.Any())
            {
                this.SaveWallet(wallet);
            }

            return removedTransactions;
        }

        /// <inheritdoc />
        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsFromDate(string walletName, DateTimeOffset fromDate)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Wallet wallet = this.GetWallet(walletName);

            var removedTransactions = new HashSet<(uint256, DateTimeOffset)>();

            lock (this.lockObject)
            {
                IEnumerable<HdAccount> accounts = wallet.GetAccountsByCoinType(this.coinType);
                foreach (HdAccount account in accounts)
                {
                    foreach (HdAddress address in account.GetCombinedAddresses())
                    {
                        var toRemove = address.Transactions.Where(t => t.CreationTime > fromDate).ToList();
                        foreach (var trx in toRemove)
                        {
                            removedTransactions.Add((trx.Id, trx.CreationTime));
                            address.Transactions.Remove(trx);
                        }
                    }
                }
            }

            if (removedTransactions.Any())
            {
                this.SaveWallet(wallet);
            }

            return removedTransactions;
        }

        /// <summary>
        /// Updates details of the last block synced in a wallet when the chain of headers finishes downloading.
        /// </summary>
        /// <param name="wallets">The wallets to update when the chain has downloaded.</param>
        /// <param name="date">The creation date of the block with which to update the wallet.</param>
        private void UpdateWhenChainDownloaded(IEnumerable<Wallet> wallets, DateTime date)
        {
            this.asyncLoopFactory.RunUntil("WalletManager.DownloadChain", this.nodeLifetime.ApplicationStopping,
                () => this.chain.IsDownloaded(),
                () =>
                {
                    int heightAtDate = this.chain.GetHeightAtTime(date);

                    foreach (Wallet wallet in wallets)
                    {
                        this.logger.LogTrace("The chain of headers has finished downloading, updating wallet '{0}' with height {1}", wallet.Name, heightAtDate);
                        this.UpdateLastBlockSyncedHeight(wallet, this.chain.GetBlock(heightAtDate));
                        this.SaveWallet(wallet);
                    }
                },
                (ex) =>
                {
                    // In case of an exception while waiting for the chain to be at a certain height, we just cut our losses and
                    // sync from the current height.
                    this.logger.LogError($"Exception occurred while waiting for chain to download: {ex.Message}");

                    foreach (Wallet wallet in wallets)
                    {
                        this.UpdateLastBlockSyncedHeight(wallet, this.chain.Tip);
                    }
                },
                TimeSpans.FiveSeconds);
        }
    }
}
