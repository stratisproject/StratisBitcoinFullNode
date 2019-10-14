using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BuilderExtensions;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using TracerAttributes;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Wallet.Tests")]
[assembly: InternalsVisibleTo("Stratis.Bitcoin.IntegrationTests")]
[assembly: InternalsVisibleTo("Stratis.Bitcoin.IntegrationTests.Common")]

namespace Stratis.Bitcoin.Features.Wallet
{
    public class WalletCollection : ICollection<Wallet>
    {
        public IWalletManager WalletManager { get; set; }

        private IWalletRepository repository => this.WalletManager?.WalletRepository;

        public int Count => this.GetWallets().Count();
        public bool IsReadOnly => true;

        private IEnumerable<Wallet> GetWallets()
        {
            foreach (string walletName in this.repository.GetWalletNames())
            {
                var wallet = this.repository.GetWallet(walletName);
                wallet.WalletManager = this.WalletManager;
                yield return wallet;
            }
        }

        public WalletCollection(WalletManager walletManager)
        {
            this.WalletManager = walletManager;
        }

        public void Add(Wallet wallet)
        {
            wallet.WalletManager = this.WalletManager;
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(Wallet wallet)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(Wallet[] arr, int index)
        {
            foreach (Wallet wallet in this.GetWallets())
                arr[index++] = wallet;
        }

        public bool Remove(Wallet wallet)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<Wallet> GetEnumerator()
        {
            return GetWallets().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetWallets().GetEnumerator();
        }
    }

    /// <summary>
    /// A manager providing operations on wallets.
    /// </summary>
    public class WalletManager : IWalletManager
    {
        /// <summary>Used to get the first account.</summary>
        public const string DefaultAccount = "account 0";

        // <summary>As per RPC method definition this should be the max allowable expiry duration.</summary>
        private const int MaxWalletUnlockDurationInSeconds = 1073741824;

        /// <summary>Quantity of accounts created in a wallet file when a wallet is restored.</summary>
        private const int WalletRecoveryAccountsCount = 1;

        /// <summary>Quantity of accounts created in a wallet file when a wallet is created.</summary>
        private const int WalletCreationAccountsCount = 1;

        /// <summary>File extension for wallet files.</summary>
        private const string WalletFileExtension = "wallet.json";

        /// <summary>Timer for saving wallet files to the file system.</summary>
        private const int WalletSavetimeIntervalInMinutes = 5;

        private const string DownloadChainLoop = "WalletManager.DownloadChain";

        /// <summary>
        /// A lock object that protects access to the <see cref="Wallet"/>.
        /// Any of the collections inside Wallet must be synchronized using this lock.
        /// </summary>
        protected readonly object lockObject;
        protected readonly object lockProcess;

        /// <summary>The async loop we need to wait upon before we can shut down this manager.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncProvider asyncProvider;

        public WalletCollection Wallets;

        /// <summary>The type of coin used in this manager.</summary>
        protected readonly CoinType coinType;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        protected readonly Network network;

        /// <summary>The chain of headers.</summary>
        protected readonly ChainIndexer ChainIndexer;

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

        /// <summary>The private key cache for unlocked wallets.</summary>
        private readonly MemoryCache privateKeyCache;

        internal bool ExcludeTransactionsFromWalletImports { get; set; }

        public IWalletRepository WalletRepository { get; private set; }

        public uint256 WalletTipHash => this.WalletCommonTip(this.ChainIndexer.Tip)?.HashBlock ?? 0;
        public int WalletTipHeight => this.WalletCommonTip(this.ChainIndexer.Tip)?.Height ?? -1;

        public WalletManager(
            ILoggerFactory loggerFactory,
            Network network,
            ChainIndexer chainIndexer,
            WalletSettings walletSettings,
            DataFolder dataFolder,
            IWalletFeePolicy walletFeePolicy,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifetime,
            IDateTimeProvider dateTimeProvider,
            IScriptAddressReader scriptAddressReader,
            IWalletRepository walletRepository,
            IBroadcasterManager broadcasterManager = null) // no need to know about transactions the node will broadcast to.
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(chainIndexer, nameof(chainIndexer));
            Guard.NotNull(walletSettings, nameof(walletSettings));
            Guard.NotNull(dataFolder, nameof(dataFolder));
            Guard.NotNull(walletFeePolicy, nameof(walletFeePolicy));
            Guard.NotNull(asyncProvider, nameof(asyncProvider));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(scriptAddressReader, nameof(scriptAddressReader));
            Guard.NotNull(walletRepository, nameof(walletRepository));

            this.Wallets = new WalletCollection(this);
            this.walletSettings = walletSettings;
            this.lockObject = new object();
            this.lockProcess = new object();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.ChainIndexer = chainIndexer;
            this.asyncProvider = asyncProvider;
            this.nodeLifetime = nodeLifetime;
            this.fileStorage = new FileStorage<Wallet>(dataFolder.WalletPath);
            this.broadcasterManager = broadcasterManager;
            this.scriptAddressReader = scriptAddressReader;
            this.dateTimeProvider = dateTimeProvider;
            this.WalletRepository = walletRepository;
            this.ExcludeTransactionsFromWalletImports = true;

            // register events
            if (this.broadcasterManager != null)
            {
                this.broadcasterManager.TransactionStateChanged += this.BroadcasterManager_TransactionStateChanged;
            }

            this.privateKeyCache = new MemoryCache(new MemoryCacheOptions() { ExpirationScanFrequency = new TimeSpan(0, 1, 0) });
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
                this.ProcessTransaction(transactionEntry.Transaction);
            }
            else
            {
                this.logger.LogDebug("Exception occurred: {0}", transactionEntry.ErrorMessage);
                this.logger.LogTrace("(-)[EXCEPTION]");
            }
        }

        public void Start()
        {
            this.WalletRepository.Initialize(false);

            // Ensure that any legacy JSON wallets are loaded to active storage.
            foreach (string walletName in this.fileStorage.GetFilesNames(WalletFileExtension))
            {
                this.logger.LogInformation("Loading legacy JSON wallet: {0}", walletName);
                this.LoadWallet(walletName.Substring(0, walletName.Length - WalletFileExtension.Length - 1));
                this.logger.LogInformation("Loading legacy JSON wallet: {0} done...", walletName);
            }

            if (this.walletSettings.IsDefaultWalletEnabled())
            {
                // Check if it already exists, if not, create one.
                if (!this.WalletRepository.GetWalletNames().Any(name => name == this.walletSettings.DefaultWalletName))
                {
                    var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
                    this.CreateWallet(this.walletSettings.DefaultWalletPassword, this.walletSettings.DefaultWalletName, string.Empty, mnemonic);
                }

                // Make sure both unlock is specified, and that we actually have a default wallet name specified.
                if (this.walletSettings.UnlockDefaultWallet)
                {
                    this.UnlockWallet(this.walletSettings.DefaultWalletPassword, this.walletSettings.DefaultWalletName, MaxWalletUnlockDurationInSeconds);
                }
            }
        }

        /// <inheritdoc />
        public void Stop()
        {
            this.WalletRepository.Shutdown();
        }

        /// <inheritdoc />
        public int GetAddressBufferSize()
        {
            return this.walletSettings.UnusedAddressesBuffer;
        }

        /// <inheritdoc />
        public (Wallet, Mnemonic) CreateWallet(string password, string name, string passphrase, Mnemonic mnemonic = null)
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

            Wallet wallet = this.WalletRepository.CreateWallet(name, encryptedSeed, extendedKey.ChainCode,
                (this.ChainIndexer.Tip == null) ? null : new HashHeightPair(this.ChainIndexer.Tip),
                this.ChainIndexer.Tip?.GetLocator(), this.ChainIndexer.Tip.Header.Time + 1);

            wallet.WalletManager = this;

            // Generate multiple accounts and addresses from the get-go.
            for (int i = 0; i < WalletCreationAccountsCount; i++)
            {
                wallet.AddNewAccount(password, i, $"account {i}");
            }

            return (wallet, mnemonic);
        }

        public ChainedHeader FindFork(string walletName, ChainedHeader chainedHeader)
        {
            return this.WalletRepository.FindFork(walletName, chainedHeader);
        }


        public ChainedHeader WalletCommonTip(ChainedHeader consensusTip)
        {
            ChainedHeader walletTip = consensusTip;

            foreach (string walletName in this.WalletRepository.GetWalletNames())
            {
                if (walletTip == null)
                    break;

                ChainedHeader fork = this.WalletRepository.FindFork(walletName, this.ChainIndexer.Tip);
                walletTip = (fork == null) ? null : walletTip.FindFork(fork);
            }

            return walletTip;
        }

        public void UpdateLastBlockSyncedHeight(ChainedHeader tip, string walletName = null)
        {
            if (walletName != null)
                this.RewindWallet(walletName, tip);
            else
                foreach (string wallet in this.GetWalletsNames())
                    this.RewindWallet(wallet, tip);
        }

        public void RewindWallet(string walletName, ChainedHeader chainedHeader)
        {
            this.WalletRepository.RewindWallet(walletName, chainedHeader);
        }

        /// <inheritdoc />
        public string SignMessage(string password, string walletName, string externalAddress, string message)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotEmpty(message, nameof(message));
            Guard.NotEmpty(externalAddress, nameof(externalAddress));

            // Get wallet
            Wallet wallet = this.GetWallet(walletName);

            // Sign the message.
            HdAddress hdAddress = this.WalletRepository.GetAccounts(wallet).SelectMany(a => this.WalletRepository.GetUsedAddresses(
                new WalletAccountReference(walletName, a.Name), int.MaxValue, false)).FirstOrDefault(addr => addr.Address.ToString() == externalAddress);

            Key privateKey = wallet.GetExtendedPrivateKeyForAddress(password, hdAddress).PrivateKey;
            return privateKey.SignMessage(message);
        }

        /// <inheritdoc />
        public bool VerifySignedMessage(string externalAddress, string message, string signature)
        {
            Guard.NotEmpty(message, nameof(message));
            Guard.NotEmpty(externalAddress, nameof(externalAddress));
            Guard.NotEmpty(signature, nameof(signature));

            bool result = false;

            try
            {
                BitcoinPubKeyAddress bitcoinPubKeyAddress = new BitcoinPubKeyAddress(externalAddress, this.network);
                result = bitcoinPubKeyAddress.VerifyMessage(message, signature);
            }
            catch (Exception ex)
            {
                this.logger.LogDebug("Failed to verify message: {0}", ex.ToString());
                this.logger.LogTrace("(-)[EXCEPTION]");
            }
            return result;
        }

        /// <inheritdoc />
        public Wallet LoadWallet(string password, string name)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));

            void CheckThePassword(Wallet walletToCheck)
            {
                // Check the password.
                try
                {
                    if (!string.IsNullOrEmpty(walletToCheck.EncryptedSeed))
                        Key.Parse(walletToCheck.EncryptedSeed, password, this.network);
                }
                catch (Exception ex)
                {
                    this.logger.LogDebug("Exception occurred: {0}", ex.ToString());
                    this.logger.LogTrace("(-)[EXCEPTION]");
                    throw new SecurityException(ex.Message);
                }
            }

            return LoadWallet(name, (wallet) => CheckThePassword(wallet));
        }

        internal Wallet LoadWallet(string walletName, Action<Wallet> check = null)
        {
            Wallet wallet = null;
            try
            {
                // Get the wallet from the repository.
                wallet = this.GetWallet(walletName);

                check?.Invoke(wallet);
            }
            catch (WalletException)
            {
                // If its not found then see if we have a JSON file for it.
                string fileName = $"{walletName}.{WalletFileExtension}";

                if (!this.fileStorage.Exists(fileName))
                    throw;

                Wallet jsonWallet = this.fileStorage.LoadByFileName(fileName);

                check?.Invoke(jsonWallet);

                var blockDict = new Dictionary<int, uint256>();

                // Only load transactions that have block hashes found in the consensus chain.
                // Let the asynchronous sync take care of the rest.
                IEnumerable<TransactionData> PreprocessTransactions(TransactionCollection transactions)
                {
                    foreach (TransactionData txData in transactions)
                    {
                        if (txData.BlockHeight != null)
                        {
                            if ((int)txData.BlockHeight > this.ChainIndexer.Tip.Height)
                                continue;

                            if (!blockDict.TryGetValue((int)txData.BlockHeight, out uint256 blockHash2))
                            {
                                blockHash2 = this.ChainIndexer.GetHeader((int)txData.BlockHeight).HashBlock;
                                blockDict[(int)txData.BlockHeight] = blockHash2;
                            }

                            if (txData.BlockHash != blockHash2)
                                continue;
                        }

                        if (txData.SpendingDetails?.BlockHeight == null)
                        {
                            yield return txData;
                            continue;
                        }

                        if ((int)txData.SpendingDetails.BlockHeight > this.ChainIndexer.Tip.Height)
                            continue;

                        if (!blockDict.TryGetValue((int)txData.SpendingDetails.BlockHeight, out uint256 blockHash))
                        {
                            blockHash = this.ChainIndexer.GetHeader((int)txData.SpendingDetails.BlockHeight).HashBlock;
                            blockDict[(int)txData.SpendingDetails.BlockHeight] = blockHash;
                        }

                        if (txData.SpendingDetails.BlockHash == null)
                            txData.SpendingDetails.BlockHash = blockHash;
                        else if (txData.SpendingDetails.BlockHash != blockHash)
                            continue;

                        yield return txData;
                    }
                }

                if (this.ExcludeTransactionsFromWalletImports)
                {
                    // Import the wallet to the database.
                    int lastBlock = 0;
                    var lastBlockSynced = new HashHeightPair(this.network.GenesisHash, lastBlock);
                    var blockLocator = this.ChainIndexer.GetHeader(lastBlock).GetLocator();

                    ITransactionContext transactionContext = this.WalletRepository.BeginTransaction(jsonWallet.Name);
                    try
                    {
                        this.WalletRepository.CreateWallet(jsonWallet.Name, jsonWallet.EncryptedSeed, jsonWallet.ChainCode, lastBlockSynced,
                            blockLocator, jsonWallet.CreationTime.ToUnixTimeSeconds());

                        wallet = this.GetWallet(jsonWallet.Name);

                        foreach (HdAccount account in jsonWallet.AccountsRoot.First().Accounts)
                        {
                            int lastUsedExternalAddress = account.ExternalAddresses.LastOrDefault(p => p.Transactions.Any())?.Index ?? -1;
                            int lastUsedInternalAddress = account.InternalAddresses.LastOrDefault(p => p.Transactions.Any())?.Index ?? -1;
                            int buffer = this.walletSettings?.UnusedAddressesBuffer ?? 20;

                            this.WalletRepository.CreateAccount(jsonWallet.Name, account.Index, account.Name, ExtPubKey.Parse(account.ExtendedPubKey), account.CreationTime,
                                (lastUsedExternalAddress + 1 + buffer, lastUsedInternalAddress + 1 + buffer));
                        }

                        transactionContext.Commit();
                    }
                    catch (Exception)
                    {
                        this.logger.LogError("Failed to import wallet '{0}'. Move the wallet to a backup location and use wallet recovery.", jsonWallet.Name);
                        transactionContext.Rollback();
                        throw;
                    }
                }
                else
                {
                    var walletTip = this.GetFork(jsonWallet, this.ChainIndexer.Tip);
                    var accountRoot = jsonWallet.AccountsRoot.First();
                    var lastBlockSynced = new HashHeightPair(accountRoot.LastBlockSyncedHash, (int)accountRoot.LastBlockSyncedHeight);
                    var blockLocator = new BlockLocator() { Blocks = jsonWallet.BlockLocator.ToList() };

                    ITransactionContext transactionContext = this.WalletRepository.BeginTransaction(jsonWallet.Name);
                    try
                    {
                        this.WalletRepository.CreateWallet(jsonWallet.Name, jsonWallet.EncryptedSeed, jsonWallet.ChainCode, lastBlockSynced,
                            blockLocator, jsonWallet.CreationTime.ToUnixTimeSeconds());

                        wallet = this.GetWallet(jsonWallet.Name);

                        foreach (HdAccount account in jsonWallet.AccountsRoot.First().Accounts)
                        {
                            int lastUsedExternalAddress = account.ExternalAddresses.LastOrDefault(p => p.Transactions.Any())?.Index ?? -1;
                            int lastUsedInternalAddress = account.InternalAddresses.LastOrDefault(p => p.Transactions.Any())?.Index ?? -1;
                            int buffer = this.walletSettings?.UnusedAddressesBuffer ?? 20;

                            this.WalletRepository.CreateAccount(jsonWallet.Name, account.Index, account.Name, ExtPubKey.Parse(account.ExtendedPubKey), account.CreationTime,
                                (lastUsedExternalAddress + 1 + buffer, lastUsedInternalAddress + 1 + buffer));

                            foreach (HdAddress address in account.ExternalAddresses)
                            {
                                this.WalletRepository.AddWatchOnlyTransactions(jsonWallet.Name, account.Name, address, address.Transactions, true);
                            }

                            foreach (HdAddress address in account.InternalAddresses)
                            {
                                this.WalletRepository.AddWatchOnlyTransactions(jsonWallet.Name, account.Name, address, address.Transactions, true);
                            }
                        }

                        transactionContext.Commit();
                    }
                    catch (Exception)
                    {
                        this.logger.LogError("Failed to import wallet '{0}'. Move the wallet to a backup location and use wallet recovery.", jsonWallet.Name);
                        transactionContext.Rollback();
                        throw;
                    }
                }
            }

            return wallet;
        }

        internal ChainedHeader GetFork(Wallet wallet, ChainedHeader chainTip)
        {
            if (chainTip == null)
                return null;

            AccountRoot accountRoot = wallet.AccountsRoot.First();

            if (chainTip.Height > accountRoot.LastBlockSyncedHeight)
            {
                if (accountRoot.LastBlockSyncedHeight < 0)
                    return null;

                chainTip = chainTip.GetAncestor((int)accountRoot.LastBlockSyncedHeight);
            }

            if (chainTip.Height == accountRoot.LastBlockSyncedHeight)
            {
                if (chainTip.HashBlock == accountRoot.LastBlockSyncedHash)
                    return chainTip;
                else
                    return null;
            }

            var blockLocator = new BlockLocator()
            {
                Blocks = wallet.BlockLocator.ToList()
            };

            List<int> locatorHeights = GetLocatorHeights(accountRoot.LastBlockSyncedHeight);

            for (int i = 0; i < locatorHeights.Count; i++)
            {
                if (chainTip.Height > locatorHeights[i])
                    chainTip = chainTip.GetAncestor(locatorHeights[i]);

                if (chainTip.HashBlock == blockLocator.Blocks[i])
                    return chainTip;
            }

            return null;
        }

        public static List<int> GetLocatorHeights(int? tipHeight)
        {
            int nStep = 1;
            var blockHeights = new List<int>();

            while (tipHeight != null)
            {
                blockHeights.Add((int)tipHeight);

                // Stop when we have added the genesis block.
                if (tipHeight == 0)
                    break;

                // Exponentially larger steps back, plus the genesis block.
                tipHeight = Math.Max((int)tipHeight - nStep, 0);

                if (blockHeights.Count > 10)
                    nStep *= 2;
            }

            return blockHeights;
        }

        /// <inheritdoc />
        public void UnlockWallet(string password, string name, int timeout)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));

            // Length of expiry of the unlocking, restricted to max duration.
            TimeSpan duration = new TimeSpan(0, 0, Math.Min(timeout, MaxWalletUnlockDurationInSeconds));

            this.CacheSecret(name, password, duration);
        }

        /// <inheritdoc />
        public void LockWallet(string name)
        {
            Guard.NotNull(name, nameof(name));

            Wallet wallet = this.GetWallet(name);
            string cacheKey = wallet.EncryptedSeed;
            this.privateKeyCache.Remove(cacheKey);
        }

        [NoTrace]
        private SecureString CacheSecret(string name, string walletPassword, TimeSpan duration)
        {
            Wallet wallet = this.GetWallet(name);
            string cacheKey = wallet.EncryptedSeed;

            if (!this.privateKeyCache.TryGetValue(cacheKey, out SecureString secretValue))
            {
                Key privateKey = Key.Parse(wallet.EncryptedSeed, walletPassword, wallet.Network);
                secretValue = privateKey.ToString(wallet.Network).ToSecureString();
            }

            this.privateKeyCache.Set(cacheKey, secretValue, duration);

            return secretValue;
        }

        /// <inheritdoc />
        public Wallet RecoverWallet(string password, string name, string mnemonic, DateTime creationTime, string passphrase, ChainedHeader lastBlockSynced = null)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));
            Guard.NotEmpty(mnemonic, nameof(mnemonic));
            Guard.NotNull(passphrase, nameof(passphrase));

            ExtKey extendedKey = HdOperations.GetExtendedKey(mnemonic, passphrase);
            string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.network).ToWif();

            // Create the wallet with the lastBlockSynced set to null to sync from the beginning.
            var wallet = new Wallet(name, encryptedSeed, extendedKey.ChainCode, creationTime, lastBlockSynced, this.WalletRepository);
            wallet.AddNewAccount(password, 0, $"account {0}");

            return wallet;
        }

        /// <inheritdoc />
        public Wallet RecoverWallet(string name, ExtPubKey extPubKey, int accountIndex, DateTime creationTime, ChainedHeader lastBlockSynced = null)
        {
            Guard.NotEmpty(name, nameof(name));
            Guard.NotNull(extPubKey, nameof(extPubKey));
            this.logger.LogDebug("({0}:'{1}',{2}:'{3}',{4}:'{5}')", nameof(name), name, nameof(extPubKey), extPubKey, nameof(accountIndex), accountIndex);

            // Create the wallet with the lastBlockSynced set to null to sync from the beginning.
            var wallet = new Wallet(name, null, null, creationTime, lastBlockSynced, this.WalletRepository);
            wallet.AddNewAccount(extPubKey, accountIndex, $"account {accountIndex}");

            return wallet;
        }

        /// <inheritdoc />
        public HdAccount GetUnusedAccount(string walletName, string password)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotEmpty(password, nameof(password));

            Wallet wallet = this.GetWallet(walletName);

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
                account = wallet.GetFirstUnusedAccount();

                if (account != null)
                {
                    this.logger.LogTrace("(-)[ACCOUNT_FOUND]");
                    return account;
                }

                // No unused account was found, create a new one.
                account = wallet.AddNewAccount(password, accountCreationTime: this.dateTimeProvider.GetTimeOffset());
            }

            return account;
        }

        public string GetExtPubKey(WalletAccountReference accountReference)
        {
            Guard.NotNull(accountReference, nameof(accountReference));

            Wallet wallet = this.GetWallet(accountReference.WalletName);

            string extPubKey;
            lock (this.lockObject)
            {
                // Get the account.
                HdAccount account = wallet.GetAccount(accountReference.AccountName);
                if (account == null)
                    throw new WalletException($"No account with the name '{accountReference.AccountName}' could be found.");
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

            return this.WalletRepository.GetUnusedAddresses(accountReference, count, isChange);
        }
        //
        //        /// <inheritdoc />
        //        public (string folderPath, IEnumerable<string>) GetWalletsFiles()
        //        {
        //            return (this.fileStorage.FolderPath, this.fileStorage.GetFilesNames(this.GetWalletFileExtension()));
        //        }

        /// <inheritdoc />
        public IEnumerable<AccountHistory> GetHistory(string walletName, string accountName = null)
        {
            Guard.NotEmpty(walletName, nameof(walletName));

            // In order to calculate the fee properly we need to retrieve all the transactions with spending details.
            Wallet wallet = this.GetWallet(walletName);

            var accountsHistory = new List<AccountHistory>();

            lock (this.lockObject)
            {
                var accounts = new List<HdAccount>();
                if (!string.IsNullOrEmpty(accountName))
                {
                    HdAccount account = wallet.GetAccount(accountName);
                    if (account == null)
                        throw new WalletException($"No account with the name '{accountName}' could be found.");

                    accounts.Add(account);
                }
                else
                {
                    accounts.AddRange(wallet.GetAccounts());
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
            lock (this.lockObject)
            {
                Wallet hdWallet = this.GetWallet(walletName);
                int tipHeight = hdWallet.AccountsRoot.First().LastBlockSyncedHeight ?? 0;

                //Need to make this work for single account but initially just scroll through accounts
                foreach (HdAccount hdAccount in this.WalletRepository.GetAccounts(hdWallet, accountName))
                {
                    (Money totalAmount, Money confirmedAmount, Money spendableAmount) = this.WalletRepository.GetAccountBalance(new WalletAccountReference(walletName, hdAccount.Name), tipHeight);

                    yield return new AccountBalance()
                    {
                        Account = hdAccount,
                        AmountConfirmed = confirmedAmount,
                        AmountUnconfirmed = totalAmount - confirmedAmount,
                        SpendableAmount = spendableAmount
                    };
                }
            }
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
                Script scriptPubKey = BitcoinAddress.Create(address, this.network).ScriptPubKey;

                foreach (string walletName in this.WalletRepository.GetWalletNames())
                {
                    if (!this.WalletRepository.GetWalletAddressLookup(walletName).Contains(scriptPubKey, out AddressIdentifier addressIdentifier))
                        continue;

                    Wallet wallet = this.WalletRepository.GetWallet(walletName);

                    string accountName = wallet.AccountsRoot.First().Accounts.FirstOrDefault(a => a.Index == addressIdentifier.AccountIndex)?.Name;

                    (Money amountTotal, Money amountConfirmed, Money amountSpendable) result =
                        this.WalletRepository.GetAccountBalance(new WalletAccountReference(walletName, accountName),
                        this.ChainIndexer.Height,
                        address: ((int)addressIdentifier.AddressType, (int)addressIdentifier.AddressIndex));

                    balance.AmountConfirmed = result.amountConfirmed;
                    balance.AmountUnconfirmed = result.amountTotal - result.amountConfirmed;
                    balance.SpendableAmount = result.amountSpendable;

                    return balance;
                }

                this.logger.LogTrace("(-)[ADDRESS_NOT_FOUND]");
                throw new WalletException($"Address '{address}' not found in wallets.");
            }
        }

        /// <inheritdoc />
        public IEnumerable<HdAccount> GetAccounts(string walletName)
        {
            Guard.NotEmpty(walletName, nameof(walletName));

            Wallet wallet = this.GetWallet(walletName);

            HdAccount[] res = null;
            lock (this.lockObject)
            {
                res = wallet.GetAccounts().ToArray();
            }
            return res;
        }

        /// <inheritdoc />
        public int LastBlockHeight()
        {
            return this.WalletTipHeight;
        }

        /// <inheritdoc />
        public bool ContainsWallets => this.WalletRepository.GetWalletNames().Any();

        /// WALLET TODO: We can remove this walletManager.LastReceivedBlockInfo()
        /// <summary>
        /// Gets the hash of the last block received by the wallets.
        /// </summary>
        /// <returns>Hash of the last block received by the wallets.</returns>
        public HashHeightPair LastReceivedBlockInfo()
        {
            if (this.Wallets.Count == 0)
                return new HashHeightPair(this.ChainIndexer.Tip);

            return new HashHeightPair(this.WalletTipHash, this.WalletTipHeight);
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

            Wallet wallet = this.GetWallet(walletName);
            UnspentOutputReference[] res = null;
            lock (this.lockObject)
            {
                res = wallet.GetAllSpendableTransactions(this.ChainIndexer.Tip.Height, confirmations, accountFilter).ToArray();
            }

            return res;
        }

        /// <inheritdoc />
        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference walletAccountReference, int confirmations = 0)
        {
            Guard.NotNull(walletAccountReference, nameof(walletAccountReference));

            Wallet wallet = this.GetWallet(walletAccountReference.WalletName);
            UnspentOutputReference[] res = null;
            lock (this.lockObject)
            {
                HdAccount account = wallet.GetAccount(walletAccountReference.AccountName);

                if (account == null)
                {
                    this.logger.LogTrace("(-)[ACT_NOT_FOUND]");
                    throw new WalletException(
                        $"Account '{walletAccountReference.AccountName}' in wallet '{walletAccountReference.WalletName}' not found.");
                }

                res = account.GetSpendableTransactions(this.ChainIndexer.Tip.Height, this.network.Consensus.CoinbaseMaturity, confirmations).ToArray();
            }

            return res;
        }

        /// <inheritdoc />
        public void RemoveBlocks(ChainedHeader fork)
        {
            lock (this.lockObject)
            {
                foreach (string walletName in this.WalletRepository.GetWalletNames())
                    this.WalletRepository.RewindWallet(walletName, fork);
            }
        }

        /// <inheritdoc />
        public void ProcessBlocks(Func<int, IEnumerable<(ChainedHeader, Block)>> blockProvider)
        {
            lock (this.lockProcess)
            {
                try
                {
                    var walletNames = this.WalletRepository.GetWalletNames().ToList();

                    // Nothing to do?
                    if (walletNames.Count == 0)
                        return;

                    ChainedHeader walletTip = this.ChainIndexer.Tip;

                    foreach (string walletName in walletNames)
                    {
                        // A wallet ahead of consensus should be truncated.
                        ChainedHeader fork = this.WalletRepository.FindFork(walletName, this.ChainIndexer.Tip);

                        if (fork?.HashBlock != this.ChainIndexer.Tip?.HashBlock)
                            this.WalletRepository.RewindWallet(walletName, fork);

                        // Update the lowest common tip.
                        walletTip = (fork == null) ? null : walletTip?.FindFork(fork);
                    }

                    // First process genesis.
                    if (walletTip == null)
                    {
                        var block0 = this.network.CreateBlock();
                        block0.Header.HashPrevBlock = null;
                        var header0 = new ChainedHeader(block0.Header, this.network.GenesisHash, 0);

                        this.WalletRepository.ProcessBlock(block0, header0);

                        walletTip = header0;
                    }

                    this.WalletRepository.ProcessBlocks(blockProvider(walletTip.Height + 1));
                }
                catch (Exception err)
                {
                    // TODO: Resolve SQLite "Locked" issue.
                    throw err;
                }
            }
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block, ChainedHeader chainedHeader = null)
        {
            Guard.NotNull(block, nameof(block));

            if (!this.ContainsWallets)
            {
                this.logger.LogTrace("(-)[NO_WALLET]");
                return;
            }

            chainedHeader = chainedHeader ?? this.ChainIndexer.GetHeader(block.GetHash());

            this.ProcessBlocks((height) => (height == chainedHeader.Height) ? new[] { (chainedHeader, block) } : new (ChainedHeader, Block)[] { });
        }

        /// <inheritdoc />
        public bool ProcessTransaction(Transaction transaction)
        {
            lock (this.lockProcess)
            {
                foreach (string walletName in this.WalletRepository.GetWalletNames())
                    this.WalletRepository.ProcessTransaction(walletName, transaction);
            }

            return true;
        }

        /// <inheritdoc />
        public void DeleteWallet(string walletName)
        {
            lock (this.lockObject)
            {
                // Back-up to JSON ".wallet.json.bak" first.
                this.SaveWallet(walletName, true);

                // Delete any JSON wallet file that may otherwise be re-imported.
                string fileName = $"{walletName}.{WalletFileExtension}";
                this.fileStorage.DeleteFile(fileName);

                // Delete from the repository.
                this.WalletRepository.DeleteWallet(walletName);
            }
        }

        private void SaveWallet(string walletName, bool toBackup)
        {
            Guard.NotNull(walletName, nameof(walletName));

            lock (this.lockObject)
            {
                var wallet = this.GetWallet(walletName);
                string fileName = $"{wallet.Name}.{WalletFileExtension}";

                if (toBackup)
                    fileName += ".bak";

                this.fileStorage.SaveToFile(wallet, fileName, new FileStorageOption { SerializeNullValues = false });
            }
        }

        /// <inheritdoc />
        public void SaveWallet(string walletName)
        {
            this.SaveWallet(walletName, false);
        }

        /// <inheritdoc />
        public string GetWalletFileExtension()
        {
            return WalletFileExtension;
        }

        /// <inheritdoc />
        public IEnumerable<string> GetWalletsNames()
        {
            return this.WalletRepository.GetWalletNames();
        }

        /// <inheritdoc />
        public Wallet GetWallet(string walletName)
        {
            var wallet = this.WalletRepository.GetWallet(walletName);
            wallet.WalletManager = this;
            return wallet;
        }

        /// <inheritdoc />
        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsByIds(string walletName, IEnumerable<uint256> transactionsIds)
        {
            Guard.NotNull(transactionsIds, nameof(transactionsIds));
            Guard.NotEmpty(walletName, nameof(walletName));

            var result = new HashSet<(uint256, DateTimeOffset)>();

            foreach (uint256 transactionId in transactionsIds)
            {
                DateTimeOffset? dateTimeOffset = this.WalletRepository.RemoveUnconfirmedTransaction(walletName, transactionId);
                if (dateTimeOffset != null)
                    result.Add((transactionId, (DateTimeOffset)dateTimeOffset));
            }

            return result;
        }

        /// <inheritdoc />
        public HashSet<(uint256, DateTimeOffset)> RemoveAllTransactions(string walletName)
        {
            Guard.NotEmpty(walletName, nameof(walletName));

            (_, IEnumerable<(uint256 txId, DateTimeOffset creationTime)> result) = this.WalletRepository.RewindWallet(walletName, null);
            result = result.Concat(this.WalletRepository.RemoveAllUnconfirmedTransactions(walletName));

            return new HashSet<(uint256, DateTimeOffset)>(result.Select(kv => (kv.txId, kv.creationTime)));
        }

        /// <inheritdoc />
        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsFromDate(string walletName, DateTimeOffset fromDate)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Wallet wallet = this.GetWallet(walletName);

            var removedTransactions = new HashSet<(uint256, DateTimeOffset)>();

            lock (this.lockObject)
            {
                IEnumerable<HdAccount> accounts = wallet.GetAccounts();
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

            return removedTransactions;
        }

        /// <inheritdoc />
        [NoTrace]
        public ExtKey GetExtKey(WalletAccountReference accountReference, string password = "", bool cache = false)
        {
            Wallet wallet = this.GetWallet(accountReference.WalletName);
            string cacheKey = wallet.EncryptedSeed;
            Key privateKey;

            if (this.privateKeyCache.TryGetValue(cacheKey, out SecureString secretValue))
            {
                privateKey = wallet.Network.CreateBitcoinSecret(secretValue.FromSecureString()).PrivateKey;
            }
            else
            {
                privateKey = Key.Parse(wallet.EncryptedSeed, password, wallet.Network);
            }

            if (cache)
            {
                // The default duration the secret is cached is 5 minutes.
                var timeOutDuration = new TimeSpan(0, 5, 0);
                this.UnlockWallet(password, accountReference.WalletName, (int)timeOutDuration.TotalSeconds);
            }

            return new ExtKey(privateKey, wallet.ChainCode);
        }

        /// <inheritdoc />
        public IEnumerable<IEnumerable<string>> GetAddressGroupings(string walletName)
        {
            return this.WalletRepository.GetAddressGroupings(walletName);
        }
    }
}
