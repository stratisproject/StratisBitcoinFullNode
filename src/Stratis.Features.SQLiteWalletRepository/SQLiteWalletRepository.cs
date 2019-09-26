using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Features.SQLiteWalletRepository.External;
using Stratis.Features.SQLiteWalletRepository.Tables;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Interfaces;
using Script = NBitcoin.Script;

[assembly: InternalsVisibleTo("Stratis.Features.SQLiteWalletRepository.Tests")]

namespace Stratis.Features.SQLiteWalletRepository
{
    /// <summary>
    /// Implements an SQLite wallet repository.
    /// </summary>
    /// <remarks>
    /// <para>This repository is basically implemented as a collection of public keys plus the transactions corresponding to those
    /// public keys. The only significant business logic being used is injected from external via the <see cref="IScriptAddressReader"/>
    /// or <see cref="IScriptDestinationReader" /> interfaces. Those interfaces bring <see cref="TxOut.ScriptPubKey" /> scripts into
    /// the world of raw public key hash (script) matching. The intention is that this will provide persistence for smart contract
    /// wallets, cold staking wallets, federation wallets and legacy wallets without any modifications to this code base.</para>
    /// <para>Federation wallets are further supported by the ability to provide a custom tx id to <see cref="ProcessTransaction" />
    /// (used only for unconfirmed transactions). In this case the custom tx id would be set to the deposit id when creating
    /// transient transactions via the <see cref="ProcessTransaction" /> call. It is expected that everything should then work
    /// as intended with confirmed transactions (via see <cref="ProcessBlock" />) taking precedence over non-confirmed transactions.</para>
    /// </remarks>
    public class SQLiteWalletRepository : IWalletRepository, IDisposable
    {
        public bool DatabasePerWallet { get; private set; }
        public bool WriteMetricsToFile { get; set; }
        public Network Network { get; private set; }

        /// <summary>Set this to true to allow adding addresses or transactions to non-watch-only wallets for testing purposes.</summary>
        public bool TestMode { get; set; }

        internal DataFolder DataFolder { get; private set; }
        internal IScriptAddressReader ScriptAddressReader { get; private set; }
        internal ConcurrentDictionary<string, WalletContainer> Wallets;
        internal string DBPath
        {
            get
            {
                return Path.Combine(this.DataFolder.WalletPath, "wallets");
            }
        }

        private readonly ILogger logger;
        private readonly IDateTimeProvider dateTimeProvider;
        private ProcessBlocksInfo processBlocksInfo;

        // Metrics.
        internal Metrics Metrics;

        public SQLiteWalletRepository(ILoggerFactory loggerFactory, DataFolder dataFolder, Network network, IDateTimeProvider dateTimeProvider, IScriptAddressReader scriptAddressReader)
        {
            this.TestMode = false;
            this.Network = network;
            this.DataFolder = dataFolder;
            this.dateTimeProvider = dateTimeProvider;
            this.ScriptAddressReader = scriptAddressReader;
            this.WriteMetricsToFile = false;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            Reset();
        }

        private void Reset()
        {
            this.Metrics = new Metrics(this.DBPath);
            this.Wallets = new ConcurrentDictionary<string, WalletContainer>();
            this.processBlocksInfo = null;
        }

        public void Dispose()
        {
        }

        private DBConnection GetConnection(string walletName = null)
        {
            if (this.DatabasePerWallet)
                Guard.NotNull(walletName, nameof(walletName));
            else if (this.processBlocksInfo != null)
            {
                this.logger.LogDebug("Re-using shared database connection");
                return this.processBlocksInfo.Conn;
            }

            if (walletName != null && this.Wallets.ContainsKey(walletName))
            {
                this.logger.LogDebug("Re-using existing database connection to wallet '{0}'", walletName);
                return this.Wallets[walletName].Conn;
            }

            if (this.DatabasePerWallet)
                this.logger.LogDebug("Creating database connection to wallet database '{0}.db'", walletName);
            else
                this.logger.LogDebug("Creating database connection to shared database `Wallet.db`");

            var conn = new DBConnection(this, this.DatabasePerWallet ? $"{walletName}.db" : "Wallet.db");

            this.logger.LogDebug("Creating database structure.");

            conn.Execute("PRAGMA temp_store = MEMORY");
            conn.Execute("PRAGMA cache_size = 100000");

            conn.BeginTransaction();
            conn.CreateDBStructure();
            conn.Commit();

            return conn;
        }

        /// <inheritdoc />
        public void Initialize(bool dbPerWallet = true)
        {
            Reset();

            Directory.CreateDirectory(this.DBPath);

            this.DatabasePerWallet = dbPerWallet;

            this.logger.LogDebug("Adding wallets found at '{0}' to wallet collection.", this.DBPath);

            if (this.DatabasePerWallet)
            {
                foreach (string walletName in Directory.EnumerateFiles(this.DBPath, "*.db")
                    .Select(p => p.Substring(this.DBPath.Length + 1).Split('.')[0]))
                {
                    var conn = GetConnection(walletName);

                    HDWallet wallet = conn.GetWalletByName(walletName);
                    var walletContainer = new WalletContainer(conn, wallet, new ProcessBlocksInfo(conn, null, wallet));
                    this.Wallets[walletName] = walletContainer;

                    walletContainer.AddressesOfInterest.AddAll(wallet.WalletId);
                    walletContainer.TransactionsOfInterest.AddAll(wallet.WalletId);

                    this.logger.LogDebug("Added '{0}` to wallet collection.", wallet.Name);
                }
            }
            else
            {
                var conn = GetConnection();

                this.processBlocksInfo = new ProcessBlocksInfo(conn, null);

                foreach (HDWallet wallet in HDWallet.GetAll(conn))
                {
                    var walletContainer = new WalletContainer(conn, wallet, this.processBlocksInfo);
                    this.Wallets[wallet.Name] = walletContainer;

                    walletContainer.AddressesOfInterest.AddAll(wallet.WalletId);
                    walletContainer.TransactionsOfInterest.AddAll(wallet.WalletId);

                    this.logger.LogDebug("Added '{0}` to wallet collection.", wallet.Name);
                }
            }
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            if (this.DatabasePerWallet)
            {
                foreach (string walletName in Directory.EnumerateFiles(this.DBPath, "*.db")
                    .Select(p => p.Substring(this.DBPath.Length + 1).Split('.')[0]))
                {
                    DBConnection conn = GetConnection(walletName);

                    if (conn.IsInTransaction)
                        conn.Rollback();

                    conn.Close();
                }
            }
            else
            {
                DBConnection conn = this.processBlocksInfo?.Conn;
                if (conn != null)
                {
                    if (conn.IsInTransaction)
                        conn.Rollback();

                    conn.Close();
                }
            }
        }

        /// <inheritdoc />
        public List<string> GetWalletNames()
        {
            var wallets = new List<string>();
            foreach (var wallet in this.Wallets)
            {
                wallets.Add(wallet.Key);
            }

            return wallets;
        }

        /// <inheritdoc />
        public Wallet GetWallet(string walletName)
        {
            WalletContainer walletContainer = this.GetWalletContainer(walletName);
            (HDWallet wallet, DBConnection conn) = (walletContainer.Wallet, walletContainer.Conn);

            var res = new Wallet()
            {
                Name = walletName,
                EncryptedSeed = wallet.EncryptedSeed,
                ChainCode = (wallet.ChainCode == null) ? null : Convert.FromBase64String(wallet.ChainCode),
                BlockLocator = wallet.BlockLocator.Split(',').Where(s => !string.IsNullOrEmpty(s)).Select(strHash => uint256.Parse(strHash)).ToList(),
                CreationTime = DateTimeOffset.FromUnixTimeSeconds(wallet.CreationTime)
            };

            res.AccountsRoot = new List<AccountRoot>();
            res.AccountsRoot.Add(new AccountRoot()
            {
                LastBlockSyncedHeight = wallet.LastBlockSyncedHeight,
                LastBlockSyncedHash = (wallet.LastBlockSyncedHash == null) ? null : uint256.Parse(wallet.LastBlockSyncedHash),
                CoinType = (CoinType)this.Network.Consensus.CoinType
            });

            return res;
        }

        /// <inheritdoc />
        public ChainedHeader FindFork(string walletName, ChainedHeader chainTip)
        {
            return this.GetWalletContainer(walletName).Wallet.GetFork(chainTip);
        }

        /// <inheritdoc />
        public (bool, IEnumerable<(uint256, DateTimeOffset)>) RewindWallet(string walletName, ChainedHeader lastBlockSynced)
        {
            WalletContainer walletContainer = this.GetWalletContainer(walletName);

            HDWallet wallet = walletContainer.Wallet;

            // If nothing to do then exit. Can't rewind any further.
            if (wallet.LastBlockSyncedHeight < 0)
                return (false, new List<(uint256, DateTimeOffset)>());

            // If nothing to do then exit. Tips match.
            if (lastBlockSynced?.HashBlock?.ToString() == wallet.LastBlockSyncedHash &&
                lastBlockSynced?.Height == wallet.LastBlockSyncedHeight)
                return (false, new List<(uint256, DateTimeOffset)>());

            // If the rewind location is not conceivably "within" the wallet then "rewindng" may actually advance the wallet
            // and lead to integrity issues. Prevent that from happening.
            if (!wallet.WalletContainsBlock(lastBlockSynced))
                return (false, new List<(uint256, DateTimeOffset)>());

            // Ok seems safe. Adjust the tip and rewind relevant transactions.
            walletContainer.WriteLockWait();

            DBConnection conn = null;
            try
            {
                conn = this.GetConnection(walletName);
                conn.BeginTransaction();
                IEnumerable<(string txId, long creationTime)> res = conn.SetLastBlockSynced(wallet, lastBlockSynced);
                conn.Commit();

                if (lastBlockSynced == null)
                    this.logger.LogDebug("Wallet {0} rewound to start.", walletName);
                else
                    this.logger.LogDebug("Wallet {0} rewound to height {1} (hash='{2}').", walletName, lastBlockSynced.Height, lastBlockSynced.HashBlock);

                return (true, res.Select(i => (uint256.Parse(i.txId), DateTimeOffset.FromUnixTimeSeconds(i.creationTime))));
            }
            catch (Exception)
            {
                conn?.Rollback();
                throw;
            }
            finally
            {
                walletContainer.WriteLockRelease();
            }
        }

        public Wallet CreateWallet(string walletName, string encryptedSeed = null, byte[] chainCode = null, HashHeightPair lastBlockSynced = null, BlockLocator blockLocator = null, long? creationTime = null)
        {
            var wallet = new HDWallet()
            {
                Name = walletName,
                EncryptedSeed = encryptedSeed,
                ChainCode = (chainCode == null) ? null : Convert.ToBase64String(chainCode),
                CreationTime = creationTime ?? (int)this.Network.GenesisTime
            };

            wallet.SetLastBlockSynced(lastBlockSynced, blockLocator, this.Network);

            DBConnection conn = GetConnection(walletName);

            this.logger.LogDebug("Creating wallet '{0}'.", walletName);

            conn.BeginTransaction();

            try
            {
                if (encryptedSeed != null && HDWallet.GetWalletByEncryptedSeed(conn, encryptedSeed) != null)
                    throw new WalletException("Cannot create this wallet as a wallet with the same private key already exists.");
                if (HDWallet.GetByName(conn, walletName) != null)
                    throw new WalletException($"Wallet with name '{walletName}' already exists.");

                wallet.CreateWallet(conn);
                conn.Commit();
            }
            catch (Exception)
            {
                conn.Rollback();
                throw;
            }

            this.logger.LogDebug("Adding wallet '{0}' to wallet collection.", walletName);

            WalletContainer walletContainer;
            if (this.DatabasePerWallet)
                walletContainer = new WalletContainer(conn, wallet);
            else
                walletContainer = new WalletContainer(conn, wallet, this.processBlocksInfo);

            this.Wallets[wallet.Name] = walletContainer;

            if (conn.IsInTransaction)
            {
                conn.AddRollbackAction(new
                {
                    wallet.Name,
                }, (dynamic rollBackData) =>
                {
                    if (this.Wallets.TryGetValue(rollBackData.Name, out WalletContainer walletContainer2))
                    {
                        walletContainer2.WriteLockWait();

                        if (this.Wallets.TryRemove(rollBackData.Name, out WalletContainer _))
                        {
                            if (this.DatabasePerWallet)
                            {
                                walletContainer2.Conn.Close();
                                File.Delete(Path.Combine(this.DBPath, $"{walletContainer.Wallet.Name}.db"));
                            }
                        }

                        walletContainer2.WriteLockRelease();
                    }
                });
            }

            return GetWallet(walletName);
        }

        /// <inheritdoc />
        public bool DeleteWallet(string walletName)
        {
            WalletContainer walletContainer = this.GetWalletContainer(walletName);
            (HDWallet wallet, DBConnection conn) = (walletContainer.Wallet, walletContainer.Conn);

            walletContainer.WriteLockWait();

            try
            {
                this.logger.LogDebug("Deleting wallet '{0}'.", walletName);

                bool isInTransaction = conn.IsInTransaction;

                if (!this.DatabasePerWallet)
                {
                    int walletId = walletContainer.Wallet.WalletId;
                    conn.BeginTransaction();

                    this.RewindWallet(walletName, null);

                    conn.Delete<HDWallet>(walletId);

                    conn.Execute($@"
                        DELETE  FROM HDAddress
                        WHERE   WalletId = {walletId}");

                    conn.Execute($@"
                        DELETE  FROM HDAccount
                        WHERE   WalletId = {walletId}");

                    conn.Commit();
                }
                else
                {
                    conn.Close();

                    if (isInTransaction)
                        File.Move(Path.Combine(this.DBPath, $"{walletName}.db"), Path.Combine(this.DBPath, $"{walletName}.bak"));
                    else
                        File.Delete(Path.Combine(this.DBPath, $"{walletName}.db"));
                }

                if (isInTransaction)
                {
                    conn.AddRollbackAction(new
                    {
                        walletContainer = this.Wallets[walletName]
                    }, (dynamic rollBackData) =>
                    {
                        string name = rollBackData.walletContainer.Wallet.Name;

                        this.Wallets[name] = rollBackData.walletContainer;

                        if (this.DatabasePerWallet)
                        {
                            File.Move(Path.Combine(this.DBPath, $"{name}.bak"), Path.Combine(this.DBPath, $"{name}.db"));
                            walletContainer.Conn = this.GetConnection(name);
                        }
                    });

                    conn.AddCommitAction(new
                    {
                        walletContainer = this.Wallets[walletName]
                    }, (dynamic rollBackData) =>
                    {
                        if (this.DatabasePerWallet)
                        {
                            string name = rollBackData.walletContainer.Wallet.Name;
                            File.Delete(Path.Combine(this.DBPath, $"{name}.bak"));
                        }
                    });
                }

                return this.Wallets.TryRemove(walletName, out _); ;
            }
            finally
            {
                walletContainer.WriteLockRelease();
            }
        }

        /// <inheritdoc />
        public HdAccount CreateAccount(string walletName, int accountIndex, string accountName, ExtPubKey extPubKey, DateTimeOffset? creationTime = null, (int external, int change)? addressCounts = null)
        {
            WalletContainer walletContainer = this.GetWalletContainer(walletName);
            (HDWallet wallet, DBConnection conn) = (walletContainer.Wallet, walletContainer.Conn);

            walletContainer.WriteLockWait();

            try
            {
                conn.BeginTransaction();

                IEnumerable<HDAccount> accounts = conn.GetAccounts(wallet.WalletId);

                if (accounts.Any(a => a.ExtPubKey != null && ExtPubKey.Parse(a.ExtPubKey) == extPubKey))
                    throw new WalletException($"There is already an account in this wallet with this xpubkey: " + extPubKey.ToString(this.Network));

                if (accounts.Any(a => a.AccountIndex == accountIndex))
                    throw new WalletException($"There is already an account in this wallet with index: { accountIndex }");

                var account = conn.CreateAccount(wallet.WalletId, accountIndex, accountName, extPubKey?.ToString(this.Network), (creationTime ?? this.dateTimeProvider.GetTimeOffset()).ToUnixTimeSeconds());

                // Add the standard number of addresses if this is not a watch-only wallet.
                if (extPubKey != null)
                {
                    conn.CreateAddresses(account, HDAddress.Internal, addressCounts?.change ?? 20);
                    conn.CreateAddresses(account, HDAddress.External, addressCounts?.external ?? 20);
                }

                conn.Commit();

                walletContainer.AddressesOfInterest.AddAll(wallet.WalletId, accountIndex);

                return this.ToHdAccount(account);
            }
            finally
            {
                walletContainer.WriteLockRelease();
            }
        }

        internal HDAddress CreateAddress(HDAccount account, int addressType, int addressIndex)
        {
            // Retrieve the pubkey associated with the private key of this address index.
            var keyPath = new KeyPath($"{addressType}/{addressIndex}");

            Script pubKeyScript = null;
            Script scriptPubKey = null;

            if (account.ExtPubKey != null)
            {
                ExtPubKey extPubKey = account.GetExtPubKey(this.Network).Derive(keyPath);
                PubKey pubKey = extPubKey.PubKey;
                pubKeyScript = pubKey.ScriptPubKey;
                scriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(pubKey);
            }

            // Add the new address details to the list of addresses.
            return new HDAddress()
            {
                WalletId = account.WalletId,
                AccountIndex = account.AccountIndex,
                AddressType = addressType,
                AddressIndex = addressIndex,
                PubKey = pubKeyScript?.ToHex(),
                ScriptPubKey = scriptPubKey?.ToHex()
            };
        }

        /// <inheritdoc />
        public void AddWatchOnlyAddresses(string walletName, string accountName, int addressType, List<HdAddress> addresses, bool force = false)
        {
            WalletContainer walletContainer = this.GetWalletContainer(walletName);

            walletContainer.WriteLockWait();

            try
            {
                var conn = this.GetConnection(walletName);
                conn.BeginTransaction();
                HDAccount account = conn.GetAccountByName(walletName, accountName);
                if (!force && !this.TestMode && account.ExtPubKey != null)
                    throw new Exception("Addresses can only be added to watch-only accounts.");

                conn.AddAdresses(account, addressType, addresses);
                conn.Commit();

                walletContainer.AddressesOfInterest.AddAll(account.WalletId, account.AccountIndex);
            }
            finally
            {
                walletContainer.WriteLockRelease();
            }
        }

        /// <inheritdoc />
        public IEnumerable<HdAccount> GetAccounts(string walletName, string accountName = null)
        {
            WalletContainer walletContainer = this.GetWalletContainer(walletName);
            (HDWallet wallet, DBConnection conn) = (walletContainer.Wallet, walletContainer.Conn);

            foreach (HDAccount account in conn.GetAccounts(wallet.WalletId, accountName))
            {
                yield return this.ToHdAccount(account);
            }
        }

        private WalletContainer GetWalletContainer(string walletName, bool throwError = true)
        {
            if (!this.Wallets.TryGetValue(walletName, out WalletContainer walletContainer) && throwError)
                throw new WalletException($"No wallet with name '{walletName}' could be found.");

            return walletContainer;
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isChange = false)
        {
            WalletContainer walletContainer = GetWalletContainer(accountReference.WalletName);

            walletContainer.WriteLockWait();

            DBConnection conn = walletContainer.Conn;

            try
            {
                HDAccount account = conn.GetAccountByName(accountReference.WalletName, accountReference.AccountName);

                if (account == null)
                    throw new WalletException($"Account '{accountReference.AccountName}' of wallet '{accountReference.WalletName}' does not exist.");

                List<HDAddress> addresses = conn.GetUnusedAddresses(account.WalletId, account.AccountIndex, isChange ? 1 : 0, count).ToList();
                if (addresses.Count < count)
                {
                    var tracker = new TopUpTracker(conn, account.WalletId, account.AccountIndex, isChange ? 1 : 0);
                    tracker.ReadAccount();

                    conn.BeginTransaction();

                    try
                    {
                        while (addresses.Count < count)
                        {
                            AddressIdentifier addressIdentifier = tracker.CreateAddress();
                            var address = HDAddress.GetAddress(conn, addressIdentifier.WalletId, (int)addressIdentifier.AccountIndex, (int)addressIdentifier.AddressType, (int)addressIdentifier.AddressIndex);
                            addresses.Add(address);
                        }
                    }
                    finally
                    {
                        conn.Commit();
                    }

                    walletContainer.AddressesOfInterest.AddAll(account.WalletId, account.AccountIndex, isChange ? 1 : 0);
                }

                return addresses.Select(a => this.ToHdAddress(a));
            }
            finally
            {
                walletContainer.WriteLockRelease();
            }
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetUsedAddresses(WalletAccountReference accountReference, int count, bool isChange = false)
        {
            WalletContainer walletContainer = this.GetWalletContainer(accountReference.WalletName);
            DBConnection conn = walletContainer.Conn;
            HDAccount account = conn.GetAccountByName(accountReference.WalletName, accountReference.AccountName);

            if (account == null)
                throw new WalletException($"No account with the name '{accountReference.AccountName}' could be found.");

            return conn.GetUsedAddresses(account.WalletId, account.AccountIndex, isChange ? 1 : 0, count).Select(a => this.ToHdAddress(a));
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetAccountAddresses(WalletAccountReference accountReference, int addressType, int count)
        {
            WalletContainer walletContainer = this.GetWalletContainer(accountReference.WalletName);
            DBConnection conn = walletContainer.Conn;

            AddressIdentifier addressIdentifier = this.GetAddressIdentifier(accountReference.WalletName, accountReference.AccountName, addressType);

            foreach (HDAddress address in HDAddress.GetAccountAddresses(conn, addressIdentifier.WalletId, (int)addressIdentifier.AccountIndex, (int)addressIdentifier.AddressType, count))
            {
                yield return this.ToHdAddress(address);
            }
        }

        /// <inheritdoc />
        public ITransactionContext BeginTransaction(string walletName)
        {
            WalletContainer walletContainer = this.GetWalletContainer(walletName, false);

            if (walletContainer == null)
            {
                walletContainer = new WalletContainer(this.GetConnection(walletName), null);
                this.Wallets[walletName] = walletContainer;
            }

            return new TransactionContext(walletContainer.Conn);
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block, ChainedHeader header, string walletName = null)
        {
            ProcessBlocks(new[] { (header, block) }, walletName);
        }

        /// <inheritdoc />
        public void ProcessBlocks(IEnumerable<(ChainedHeader header, Block block)> blocks, string walletName = null)
        {
            if (this.Wallets.Count == 0 || !blocks.Any())
                return;

            if (this.DatabasePerWallet && walletName == null)
            {
                List<WalletContainer> rounds = this.Wallets.Values.Where(c => c.LockProcessBlocks.Wait(10)).ToList();

                if (rounds.Count > 0)
                {
                    foreach (var round in rounds)
                    {
                        round.PrevTip = null;
                        round.NewTip = null;
                    }

                    foreach ((ChainedHeader header, Block block) in blocks.Append((null, null)))
                    {
                        Parallel.ForEach(rounds, round =>
                        {
                            try
                            {
                                ParallelProcessBlock(round, block, header);
                            }
                            catch (Exception err)
                            {
                                throw err;
                            }
                            finally
                            {
                                if (header == null)
                                    round.LockProcessBlocks.Release();
                            }
                        });

                        if (header == null)
                            break;
                    }
                }
            }
            else
            {
                ProcessBlocksInfo round = (walletName != null) ? this.Wallets[walletName] : this.processBlocksInfo;

                if (round.LockProcessBlocks.Wait(10))
                {
                    round.PrevTip = null;
                    round.NewTip = null;

                    foreach ((ChainedHeader header, Block block) in blocks.Append((null, null)))
                    {
                        try
                        {
                            ParallelProcessBlock(round, block, header);
                        }
                        catch (Exception err)
                        {
                            throw err;
                        }
                        finally
                        {
                            if (header == null)
                                round.LockProcessBlocks.Release();
                        }

                        if (header == null)
                            break;
                    }
                }
            }
        }

        private void ParallelProcessBlock(ProcessBlocksInfo round, Block block, ChainedHeader header)
        {
            try
            {
                HDWallet wallet = round.Wallet;
                DBConnection conn = round.Conn;
                string lastBlockSyncedHash = (header == null) ? null : (header.Previous?.HashBlock ?? (uint256)0).ToString();

                if (round.NewTip != null)
                {
                    // Flush when new wallets are joining. This ensures that PrevTip will match all wallets requiring updating and advancing.
                    bool walletsJoining;
                    if (round.Wallet == null && !this.DatabasePerWallet)
                        walletsJoining = this.Wallets.Any(c => c.Value.Wallet.LastBlockSyncedHash == lastBlockSyncedHash);
                    else
                        walletsJoining = round.Wallet.LastBlockSyncedHash == lastBlockSyncedHash;

                    // See if other threads are waiting to update any of the wallets.
                    bool threadsWaiting = conn.TransactionLock.WaitingThreads >= 1 || round.ParticipatingWallets.Any(name => this.Wallets[name].HaveWaitingThreads);
                    if (threadsWaiting || ((round.Outputs.Count + round.PrevOuts.Count) >= 10000) || header == null || walletsJoining || DateTime.Now.Ticks >= round.NextScheduledCatchup)
                    {
                        long flagFall = DateTime.Now.Ticks;

                        if (!conn.IsInTransaction)
                        {
                            Guard.Assert(!round.MustCommit);

                            conn.BeginTransaction();
                            round.MustCommit = true;
                        }

                        if (round.Outputs.Count != 0 || round.PrevOuts.Count != 0)
                        {
                            IEnumerable<IEnumerable<string>> blockToScript = (new[] { round.Outputs, round.PrevOuts }).Select(list => list.CreateScript());

                            conn.ProcessTransactions(blockToScript, wallet, round.NewTip, round.PrevTip?.Hash ?? 0);

                            round.Outputs.Clear();
                            round.PrevOuts.Clear();

                            round.AddressesOfInterest.Confirm();
                            round.TransactionsOfInterest.Confirm();

                        }
                        else
                        {
                            HDWallet.AdvanceTip(conn, wallet, round.NewTip, round.PrevTip?.Hash ?? 0);
                        }

                        if (round.MustCommit)
                        {
                            long flagFall3 = DateTime.Now.Ticks;
                            conn.Commit();
                            this.Metrics.CommitTime += (DateTime.Now.Ticks - flagFall3);
                            round.MustCommit = false;
                        }

                        this.Metrics.ProcessTime += (DateTime.Now.Ticks - flagFall);

                        round.PrevTip = null;

                        // Update all wallets found in the DB into the containers.
                        foreach (HDWallet updatedWallet in HDWallet.GetAll(conn))
                        {
                            if (!this.Wallets.TryGetValue(updatedWallet.Name, out WalletContainer walletContainer))
                                continue;

                            walletContainer.Wallet.LastBlockSyncedHash = updatedWallet.LastBlockSyncedHash;
                            walletContainer.Wallet.LastBlockSyncedHeight = updatedWallet.LastBlockSyncedHeight;
                            walletContainer.Wallet.BlockLocator = updatedWallet.BlockLocator;
                        }

                        this.Metrics.LogMetrics(this, conn, header, wallet);

                        // Release all locks.
                        if (round.ParticipatingWallets.Count > 0)
                        {
                            foreach (string walletName in round.ParticipatingWallets)
                                this.Wallets[walletName].WriteLockRelease();
                        }

                        round.ParticipatingWallets.Clear();

                        if (DateTime.Now.Ticks >= round.NextScheduledCatchup)
                            round.NextScheduledCatchup = DateTime.Now.Ticks + 10 * 10_000_000;
                    }
                }

                if (header == null)
                    return;

                if (round.PrevTip == null)
                {
                    // Determine participating wallets.
                    if (round.Wallet == null && !this.DatabasePerWallet)
                        round.ParticipatingWallets = this.Wallets.Values.Where(c => c.Wallet.LastBlockSyncedHash == lastBlockSyncedHash).Select(c => c.Wallet.Name).ToList();
                    else if (round.Wallet.LastBlockSyncedHash == lastBlockSyncedHash)
                        round.ParticipatingWallets = new List<string> { round.Wallet.Name };
                    else
                        round.ParticipatingWallets = new List<string>();

                    // Now grab the wallet locks.
                    foreach (string walletName in round.ParticipatingWallets)
                        this.Wallets[walletName].WriteLockWait();

                    // Batch starting.
                    round.PrevTip = (header.Previous == null) ? new HashHeightPair(0, -1) : new HashHeightPair(header.Previous);
                }

                if (block != null)
                {
                    // Maintain metrics.
                    long flagFall2 = DateTime.Now.Ticks;
                    this.Metrics.BlockCount++;

                    // Determine the scripts for creating temporary tables and inserting the block's information into them.
                    ITransactionsToLists transactionsToLists = new TransactionsToLists(this.Network, this.ScriptAddressReader, round);
                    if (transactionsToLists.ProcessTransactions(block.Transactions, new HashHeightPair(header)))
                        this.Metrics.ProcessCount++;

                    this.Metrics.BlockTime += (DateTime.Now.Ticks - flagFall2);
                }

                round.NewTip = header;
            }
            catch (Exception err)
            {
                if (round.MustCommit)
                {
                    round.Conn.Rollback();
                    round.MustCommit = false;
                }

                throw;
            }
        }

        /// <inheritdoc />
        public DateTimeOffset? RemoveUnconfirmedTransaction(string walletName, uint256 txId)
        {
            WalletContainer walletContainer = this.GetWalletContainer(walletName);
            (HDWallet wallet, DBConnection conn) = (walletContainer.Wallet, walletContainer.Conn);

            walletContainer.WriteLockWait();

            try
            {
                conn.BeginTransaction();
                long? unixTimeSeconds = conn.RemoveUnconfirmedTransaction(wallet.WalletId, txId);
                conn.Commit();

                return (unixTimeSeconds == null) ? (DateTimeOffset?)null : DateTimeOffset.FromUnixTimeSeconds((long)unixTimeSeconds);
            }
            finally
            {
                walletContainer.WriteLockRelease();
            }
        }

        /// <inheritdoc />
        public IEnumerable<(uint256 txId, DateTimeOffset creationTime)> RemoveAllUnconfirmedTransactions(string walletName)
        {
            WalletContainer walletContainer = this.GetWalletContainer(walletName);
            (HDWallet wallet, DBConnection conn) = (walletContainer.Wallet, walletContainer.Conn);

            walletContainer.WriteLockWait();

            try
            {
                conn.BeginTransaction();
                IEnumerable<(string txId, long creationTime)> res = conn.RemoveAllUnconfirmedTransactions(wallet.WalletId);
                conn.Commit();

                return res.Select(i => (uint256.Parse(i.txId), DateTimeOffset.FromUnixTimeSeconds(i.creationTime)));
            }
            finally
            {
                walletContainer.WriteLockRelease();
            }
        }

        /// <inheritdoc />
        public void ProcessTransaction(string walletName, Transaction transaction, uint256 fixedTxId = null)
        {
            WalletContainer walletContainer = this.GetWalletContainer(walletName);
            (HDWallet wallet, DBConnection conn) = (walletContainer.Wallet, walletContainer.Conn);

            walletContainer.LockProcessBlocks.Wait();

            try
            {
                var processBlocksInfo = new ProcessBlocksInfo(conn, walletContainer, wallet);
                IEnumerable<IEnumerable<string>> txToScript;
                {
                    var transactionsToLists = new TransactionsToLists(this.Network, this.ScriptAddressReader, processBlocksInfo);
                    transactionsToLists.ProcessTransactions(new[] { transaction }, null, fixedTxId);
                    txToScript = (new[] { processBlocksInfo.Outputs, processBlocksInfo.PrevOuts }).Select(list => list.CreateScript());
                }

                if (!conn.IsInTransaction)
                {
                    conn.BeginTransaction();
                    processBlocksInfo.MustCommit = true;
                }

                conn.ProcessTransactions(txToScript, wallet);

                if (processBlocksInfo.MustCommit)
                    conn.Commit();
            }
            finally
            {
                walletContainer.LockProcessBlocks.Release();
            }
        }

        /// <inheritdoc />
        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference walletAccountReference, int currentChainHeight, int confirmations = 0, int? coinBaseMaturity = null)
        {
            WalletContainer walletContainer = this.GetWalletContainer(walletAccountReference.WalletName);
            DBConnection conn = walletContainer.Conn;
            HDAccount account = conn.GetAccountByName(walletAccountReference.WalletName, walletAccountReference.AccountName);

            var hdAccount = this.ToHdAccount(account);

            foreach (HDTransactionData transactionData in conn.GetSpendableOutputs(account.WalletId, account.AccountIndex, currentChainHeight, coinBaseMaturity ?? this.Network.Consensus.CoinbaseMaturity, confirmations))
            {
                var pubKeyScript = new Script(Encoders.Hex.DecodeData(transactionData.ScriptPubKey));
                PubKey pubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(pubKeyScript);

                if (transactionData.OutputBlockHeight == null)
                    continue;

                yield return new UnspentOutputReference()
                {
                    Account = hdAccount,
                    Transaction = this.ToTransactionData(transactionData, HDPayment.GetAllPayments(conn, transactionData.SpendTxTime ?? 0, transactionData.SpendTxId, transactionData.OutputTxId, transactionData.OutputIndex, transactionData.ScriptPubKey)),
                    Confirmations = (currentChainHeight + 1) - (int)transactionData.OutputBlockHeight,
                    Address = this.ToHdAddress(new HDAddress()
                    {
                        AccountIndex = transactionData.AccountIndex,
                        AddressIndex = transactionData.AddressIndex,
                        AddressType = (int)transactionData.AddressType,
                        PubKey = transactionData.ScriptPubKey,
                        ScriptPubKey = transactionData.RedeemScript
                    })
                };
            }
        }

        /// <inheritdoc />
        public (Money totalAmount, Money confirmedAmount, Money spendableAmount) GetAccountBalance(WalletAccountReference walletAccountReference, int currentChainHeight, int confirmations = 0, int? coinBaseMaturity = null)
        {
            WalletContainer walletContainer = this.GetWalletContainer(walletAccountReference.WalletName);

            DBConnection conn = walletContainer.Conn;
            HDAccount account = conn.GetAccountByName(walletAccountReference.WalletName, walletAccountReference.AccountName);

            (decimal total, decimal confirmed, decimal spendable) = HDTransactionData.GetBalance(conn, account.WalletId, account.AccountIndex, null, currentChainHeight, coinBaseMaturity ?? (int)this.Network.Consensus.CoinbaseMaturity, confirmations);

            return (new Money(total, MoneyUnit.BTC), new Money(confirmed, MoneyUnit.BTC), new Money(spendable, MoneyUnit.BTC));
        }

        /// <inheritdoc />
        public IWalletAddressReadOnlyLookup GetWalletAddressLookup(string walletName)
        {
            return this.GetWalletContainer(walletName).AddressesOfInterest;
        }

        /// <inheritdoc />
        public IWalletTransactionReadOnlyLookup GetWalletTransactionLookup(string walletName)
        {
            return this.GetWalletContainer(walletName).TransactionsOfInterest;
        }

        /// <inheritdoc />
        public IEnumerable<TransactionData> GetAllTransactions(AddressIdentifier addressIdentifier, int limit = int.MaxValue, TransactionData prev = null, bool descending = true, bool includePayments = false)
        {
            WalletContainer walletContainer = this.Wallets.Values.FirstOrDefault(wc => wc.Wallet.WalletId == addressIdentifier.WalletId);
            DBConnection conn = walletContainer.Conn;

            var prevTran = (prev == null) ? null : new HDTransactionData() {
                OutputTxTime = prev.CreationTime.ToUnixTimeSeconds(),
                OutputIndex = prev.Index
            };

            foreach (HDTransactionData tranData in HDTransactionData.GetAllTransactions(conn, addressIdentifier.WalletId,
                addressIdentifier.AccountIndex, addressIdentifier.AddressType, addressIdentifier.AddressIndex, limit, prevTran, descending))
            {
                var payments = includePayments ? HDPayment.GetAllPayments(conn, tranData.SpendTxTime ?? 0, tranData.SpendTxId, tranData.OutputTxId,
                    tranData.OutputIndex, tranData.ScriptPubKey) : new HDPayment[] { };

                yield return this.ToTransactionData(tranData, payments);
            }
        }

        /// <inheritdoc />
        public AddressIdentifier GetAddressIdentifier(string walletName, string accountName = null, int? addressType = null, int? addressIndex = null)
        {
            DBConnection conn = this.GetConnection(walletName);
            int walletId;
            int? accountIndex;

            if (accountName != null)
            {
                HDAccount account = conn.GetAccountByName(walletName, accountName);
                walletId = account.WalletId;
                accountIndex = account.AccountIndex;
            }
            else
            {
                HDWallet wallet = conn.GetWalletByName(walletName);
                walletId = wallet.WalletId;
                accountIndex = null;
            }

            return new AddressIdentifier()
            {
                WalletId = walletId,
                AccountIndex = accountIndex,
                AddressType = addressType,
                AddressIndex = addressIndex
            };
        }

        /// <inheritdoc />
        public IEnumerable<AccountHistory> GetHistory(string walletName, string accountName = null)
        {
            WalletContainer walletContainer = this.GetWalletContainer(walletName);
            (HDWallet wallet, DBConnection conn) = (walletContainer.Wallet, walletContainer.Conn);

            var accounts = new List<HDAccount>();

            foreach (HDAccount account in conn.GetAccounts(wallet.WalletId, accountName))
            {
                var history = new List<FlatHistory>();

                foreach (HDAddress address in conn.GetUsedAddresses(wallet.WalletId, account.AccountIndex, HDAddress.External, int.MaxValue)
                    .Concat(conn.GetUsedAddresses(wallet.WalletId, account.AccountIndex, HDAddress.Internal, int.MaxValue)))
                {
                    HdAddress hdAddress = this.ToHdAddress(address);

                    foreach (var transaction in conn.GetTransactionsForAddress(wallet.WalletId, account.AccountIndex, address.AddressType, address.AddressIndex))
                    {
                        history.Add(new FlatHistory()
                        {
                            Address = hdAddress,
                            Transaction = this.ToTransactionData(transaction, HDPayment.GetAllPayments(conn, transaction.SpendTxTime ?? 0, transaction.SpendTxId, transaction.OutputTxId, transaction.OutputIndex, transaction.ScriptPubKey))
                        });
                    }
                }

                yield return new AccountHistory()
                {
                    Account = this.ToHdAccount(account),
                    History = history
                };
            }
        }

        /// <inheritdoc />
        public IEnumerable<TransactionData> GetTransactionInputs(string walletName, DateTimeOffset? transactionTime, uint256 transactionId = null, bool includePayments = false)
        {
            WalletContainer walletContainer = this.GetWalletContainer(walletName);
            (HDWallet wallet, DBConnection conn) = (walletContainer.Wallet, walletContainer.Conn);

            foreach (HDTransactionData tranData in HDTransactionData.FindTransactionInputs(conn, wallet.WalletId, transactionTime?.ToUnixTimeSeconds(), transactionId?.ToString()))
            {
                var payments = includePayments ? HDPayment.GetAllPayments(conn, tranData.SpendTxTime ?? 0, tranData.SpendTxId, tranData.OutputTxId,
                    tranData.OutputIndex, tranData.ScriptPubKey) : new HDPayment[] { };

                yield return this.ToTransactionData(tranData, payments);
            }
        }

        private class ScriptTransaction
        {
            public string ScriptPubKey { get; set; }
            public string TransactionId { get; set; }
        }

        /// <inheritdoc />
        public IEnumerable<IEnumerable<string>> GetAddressGroupings(string walletName)
        {
            WalletContainer walletContainer = this.GetWalletContainer(walletName);
            (HDWallet wallet, DBConnection conn) = (walletContainer.Wallet, walletContainer.Conn);

            var addressGroupings = new Dictionary<string, HashSet<string>>();

            // Group all input addresses with each other.
            foreach (var spendData in conn.Query<ScriptTransaction>($@"
                SELECT  ScriptPubKey
                ,       SpendTxId TransactionId
                FROM    HDTransactionData
                WHERE   WalletId = {wallet.WalletId}
                AND     TransactionId IS NOT NULL"))
            {
                var spendTxId = spendData.TransactionId;
                if (spendTxId != null)
                {
                    if (!addressGroupings.TryGetValue(spendTxId, out HashSet<string> grouping))
                    {
                        grouping = new HashSet<string>();
                        addressGroupings[spendTxId] = grouping;
                    }

                    grouping.Add(spendData.ScriptPubKey);
                }
            }

            // Include any change addresses.
            foreach (var outputData in conn.Query<ScriptTransaction>($@"
                SELECT  ScriptPubKey
                ,       OutputTxId TransactionId
                FROM    HDTransactionData
                WHERE   WalletId = {wallet.WalletId}"))
            {
                if (addressGroupings.TryGetValue(outputData.TransactionId, out HashSet<string> grouping))
                    grouping.Add(outputData.ScriptPubKey);
                else
                    // Create its own grouping
                    addressGroupings[outputData.TransactionId] = new HashSet<string> { outputData.ScriptPubKey };
            }

            // Determine unique mappings.
            var uniqueGroupings = new List<HashSet<string>>();
            var setMap = new Dictionary<string, HashSet<string>>();

            foreach ((string spendTxId, HashSet<string> grouping) in addressGroupings.Select(kv => (kv.Key, kv.Value)))
            {
                // Create a list of unique groupings intersecting this grouping.
                var hits = new List<HashSet<string>>();
                foreach (string scriptPubkey in grouping)
                    if (setMap.TryGetValue(scriptPubkey, out HashSet<string> it))
                        hits.Add(it);

                // Merge the matching uinique groupings into this grouping and remove the old groupings.
                foreach (HashSet<string> hit in hits)
                {
                    grouping.UnionWith(hit);
                    uniqueGroupings.Remove(hit);
                }

                // Add the new merged grouping.
                uniqueGroupings.Add(grouping);

                // Update the set map which maps addresses to the unique grouping they appear in.
                foreach (string scriptPubKey in grouping)
                    setMap[scriptPubKey] = grouping;
            }

            // Return the result.
            foreach (HashSet<string> scriptPubKeys in uniqueGroupings)
            {
                var addressBase58s = new List<string>();

                foreach (string scriptPubKey in scriptPubKeys)
                {
                    Script script = Script.FromHex(scriptPubKey);
                    var addressBase58 = script.GetDestinationAddress(this.Network);
                    if (addressBase58 == null)
                        continue;

                    addressBase58s.Add(addressBase58.ToString());
                }

                yield return addressBase58s;
            }
        }
    }
}
