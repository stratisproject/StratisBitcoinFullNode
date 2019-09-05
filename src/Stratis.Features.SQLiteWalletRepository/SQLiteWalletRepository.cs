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

        internal Network Network { get; private set; }
        internal DataFolder DataFolder { get; private set; }
        internal IScriptAddressReader ScriptAddressReader { get; private set; }
        internal ConcurrentDictionary<string, WalletContainer> Wallets;
        internal string DBPath
        {
            get
            {
                return Path.Combine(this.DataFolder.WalletPath, nameof(SQLiteWalletRepository));
            }
        }

        private readonly ILogger logger;
        private readonly IDateTimeProvider dateTimeProvider;
        private ProcessBlocksInfo processBlocksInfo;

        // Metrics.
        internal Metrics Metrics;

        public SQLiteWalletRepository(ILoggerFactory loggerFactory, DataFolder dataFolder, Network network, IDateTimeProvider dateTimeProvider, IScriptAddressReader scriptAddressReader)
        {
            this.Network = network;
            this.DataFolder = dataFolder;
            this.dateTimeProvider = dateTimeProvider;
            this.ScriptAddressReader = scriptAddressReader;
            this.WriteMetricsToFile = false;
            this.Metrics = new Metrics(this.DBPath);

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

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

        public List<string> GetWalletNames()
        {
            return this.Wallets.Select(kv => kv.Value.Wallet.Name).ToList();
        }

        /// <inheritdoc />
        public ChainedHeader FindFork(string walletName, ChainedHeader chainTip)
        {
            WalletContainer walletContainer = this.Wallets[walletName];

            walletContainer.ReadLockWait();

            try
            {
                HDWallet wallet = walletContainer.Wallet;

                return wallet.GetFork(chainTip);
            }
            finally
            {
                walletContainer.ReadLockRelease();
            }
        }

        /// <inheritdoc />
        public void RewindWallet(string walletName, ChainedHeader lastBlockSynced)
        {
            WalletContainer walletContainer = this.Wallets[walletName];

            walletContainer.WriteLockWait();

            try
            {
                HDWallet wallet = walletContainer.Wallet;

                // Perform a sanity check that the location being set is conceivably "within" the wallet.
                if (!wallet.WalletContainsBlock(lastBlockSynced))
                {
                    this.logger.LogError("Can't rewind the wallet using the supplied tip.");
                    throw new InvalidProgramException("Can't rewind the wallet using the supplied tip.");
                }

                // Ok seems safe. Adjust the tip and rewind relevant transactions.
                DBConnection conn = this.GetConnection(walletName);
                conn.BeginTransaction();
                conn.SetLastBlockSynced(wallet, lastBlockSynced);
                conn.Commit();

                if (lastBlockSynced == null)
                    this.logger.LogDebug("Wallet {0} rewound to start.", walletName);
                else
                    this.logger.LogDebug("Wallet {0} rewound to height {1} (hash='{2}').", walletName, lastBlockSynced.Height, lastBlockSynced.HashBlock);
            }
            finally
            {
                walletContainer.WriteLockRelease();
            }
        }

        /// <inheritdoc />
        public void CreateWallet(string walletName, string encryptedSeed, byte[] chainCode, ChainedHeader lastBlockSynced = null)
        {
            int creationTime;
            if (lastBlockSynced == null)
                creationTime = (int)this.Network.GenesisTime;
            else
                creationTime = (int)lastBlockSynced.Header.Time + 1;

            var wallet = new HDWallet()
            {
                Name = walletName,
                EncryptedSeed = encryptedSeed,
                ChainCode = Convert.ToBase64String(chainCode),
                CreationTime = creationTime
            };

            wallet.SetLastBlockSynced(lastBlockSynced, this.Network);

            DBConnection conn = GetConnection(walletName);

            this.logger.LogDebug("Creating wallet '{0}'.", walletName);

            conn.BeginTransaction();
            wallet.CreateWallet(conn);
            conn.Commit();

            this.logger.LogDebug("Adding wallet '{0}' to wallet collection.", walletName);

            WalletContainer walletContainer;
            if (this.DatabasePerWallet)
                walletContainer = new WalletContainer(conn, wallet);
            else
                walletContainer = new WalletContainer(conn, wallet, this.processBlocksInfo);

            walletContainer.WriteLockWait();

            try
            {
                this.Wallets[wallet.Name] = walletContainer;

                if (conn.IsInTransaction)
                {
                    conn.AddRollbackAction(new
                    {
                        wallet.Name,
                    }, (dynamic rollBackData) =>
                    {
                        if (this.Wallets.TryRemove(rollBackData.Name, out WalletContainer walletContaner))
                        {
                            if (this.DatabasePerWallet)
                            {
                                walletContainer.Conn.Close();
                                File.Delete(Path.Combine(this.DBPath, $"{walletContainer.Wallet.Name}.db"));
                            }
                        }
                    });
                }
            }
            finally
            {
                walletContainer.WriteLockRelease();
            }
        }

        /// <inheritdoc />
        public bool DeleteWallet(string walletName)
        {
            WalletContainer walletContainer = this.Wallets[walletName];

            walletContainer.WriteLockWait();

            try
            {
                this.logger.LogDebug("Deleting wallet '{0}'.", walletName);

                DBConnection conn = GetConnection(walletName);

                bool isInTransaction = conn.IsInTransaction;

                // TODO: Delete HDPayments no longer required.
                if (!this.DatabasePerWallet)
                {
                    int walletId = walletContainer.Wallet.WalletId;
                    conn.BeginTransaction();

                    this.RewindWallet(walletName, null);

                    conn.Delete<HDWallet>(walletId);
                    conn.Execute($@"
                DELETE  FROM HDAddress
                WHERE   WalletId = {walletId}
                ");
                    conn.Execute($@"
                DELETE  FROM HDAccount
                WHERE   WalletId = {walletId}
                ");
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
        public void CreateAccount(string walletName, int accountIndex, string accountName, ExtPubKey extPubKey, DateTimeOffset? creationTime = null)
        {
            WalletContainer walletContainer = this.Wallets[walletName];

            walletContainer.WriteLockWait();

            try
            {
                HDWallet wallet = walletContainer.Wallet;

                var conn = this.GetConnection(walletName);
                conn.BeginTransaction();

                var account = conn.CreateAccount(wallet.WalletId, accountIndex, accountName, extPubKey.ToString(this.Network), (int)(creationTime ?? this.dateTimeProvider.GetTimeOffset()).ToUnixTimeSeconds());
                conn.CreateAddresses(account, HDAddress.Internal, HDAddress.StandardAddressBuffer);
                conn.CreateAddresses(account, HDAddress.External, HDAddress.StandardAddressBuffer);
                conn.Commit();

                walletContainer.AddressesOfInterest.AddAll(wallet.WalletId, accountIndex);
            }
            finally
            {
                walletContainer.WriteLockRelease();
            }
        }

        internal void AddAddresses(string walletName, string accountName, int addressType, List<Script> addresses)
        {
            WalletContainer walletContainer = this.Wallets[walletName];

            walletContainer.WriteLockWait();

            try
            {
                var conn = this.GetConnection(walletName);
                conn.BeginTransaction();
                HDAccount account = conn.GetAccountByName(walletName, accountName);
                conn.AddAdresses(account, addressType, addresses);
                conn.Commit();

                walletContainer.AddressesOfInterest.AddAll(account.WalletId, account.AccountIndex, addressType);
            }
            finally
            {
                walletContainer.WriteLockRelease();
            }
        }

        /// <inheritdoc />
        public void CreateAccount(string walletName, int accountIndex, string accountName, string password, DateTimeOffset? creationTime = null)
        {
            WalletContainer walletContainer = this.Wallets[walletName];

            walletContainer.WriteLockWait();

            try
            {
                HDWallet wallet = walletContainer.Wallet;

                ExtPubKey extPubKey;

                var conn = this.GetConnection(walletName);

                // Get the extended pub key used to generate addresses for this account.
                // Not passing extPubKey into the method to guarantee DB integrity.
                Key privateKey = Key.Parse(wallet.EncryptedSeed, password, this.Network);
                var seedExtKey = new ExtKey(privateKey, Convert.FromBase64String(wallet.ChainCode));
                ExtKey addressExtKey = seedExtKey.Derive(new KeyPath(this.ToHdPath(accountIndex)));
                extPubKey = addressExtKey.Neuter();

                this.CreateAccount(walletName, accountIndex, accountName, extPubKey, creationTime);
            }
            finally
            {
                walletContainer.WriteLockRelease();
            }
        }

        public IEnumerable<HdAccount> GetAccounts(string walletName)
        {
            var conn = this.GetConnection(walletName);

            HDWallet wallet = conn.GetWalletByName(walletName);

            foreach (HDAccount account in conn.GetAccounts(wallet.WalletId))
                yield return this.ToHdAccount(account);
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isChange = false)
        {
            var conn = this.GetConnection(accountReference.WalletName);

            var account = conn.GetAccountByName(accountReference.WalletName, accountReference.AccountName);

            return conn.GetUnusedAddresses(account.WalletId, account.AccountIndex, isChange ? 1 : 0, count).Select(a => this.ToHdAddress(a));
        }

        /// <inheritdoc />
        public ITransactionContext BeginTransaction(string walletName)
        {
            DBConnection conn = this.GetConnection(walletName);
            if (!this.Wallets.TryGetValue(walletName, out WalletContainer walletContainer))
            {
                walletContainer = new WalletContainer(conn, null);
                this.Wallets[walletName] = walletContainer;
            }

            var res = new TransactionContext(conn);
            conn.BeginTransaction();
            return res;
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block, ChainedHeader header, string walletName = null)
        {
            ProcessBlocks(new[] { (header, block) }, walletName);
        }

        /// <inheritdoc />
        public void ProcessBlocks(IEnumerable<(ChainedHeader header, Block block)> blocks, string walletName = null)
        {
            if (this.Wallets.Count == 0)
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

                        if (!round.MustCommit && !conn.IsInTransaction)
                        {
                            conn.BeginTransaction();
                            round.MustCommit = true;
                        }

                        if (round.Outputs.Count != 0 || round.PrevOuts.Count != 0)
                        {
                            IEnumerable<IEnumerable<string>> blockToScript = (new[] { round.Outputs, round.PrevOuts }).Select(list => list.CreateScript());

                            conn.ProcessTransactions(blockToScript, wallet, round.NewTip, round.PrevTip);

                            round.Outputs.Clear();
                            round.PrevOuts.Clear();

                            round.AddressesOfInterest.Confirm();
                            round.TransactionsOfInterest.Confirm();

                        }
                        else
                        {
                            HDWallet.AdvanceTip(conn, wallet, round.NewTip, round.PrevTip);
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
                    if (transactionsToLists.ProcessTransactions(block.Transactions, header))
                        this.Metrics.ProcessCount++;

                    this.Metrics.BlockTime += (DateTime.Now.Ticks - flagFall2);
                }

                round.NewTip = header;
            }
            catch (Exception)
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
        public void RemoveUnconfirmedTransaction(string walletName, uint256 txId)
        {
            WalletContainer walletContainer = this.Wallets[walletName];
            walletContainer.WriteLockWait();

            try
            {
                DBConnection conn = this.GetConnection(walletName);
                HDWallet wallet = conn.GetWalletByName(walletName);
                conn.BeginTransaction();
                conn.RemoveUnconfirmedTransaction(wallet.WalletId, txId);
                conn.Commit();
            }
            finally
            {
                walletContainer.WriteLockRelease();
            }
        }

        /// <inheritdoc />
        public void ProcessTransaction(string walletName, Transaction transaction, uint256 fixedTxId = null)
        {
            WalletContainer walletContainer = this.Wallets[walletName];

            walletContainer.LockProcessBlocks.Wait();

            try
            {
                HDWallet wallet = walletContainer.Wallet;
                DBConnection conn = this.GetConnection(walletName);
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
        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference walletAccountReference, int currentChainHeight, int confirmations = 0)
        {
            DBConnection conn = this.GetConnection(walletAccountReference.WalletName);
            HDAccount account = conn.GetAccountByName(walletAccountReference.WalletName, walletAccountReference.AccountName);

            var hdAccount = this.ToHdAccount(account);

            foreach (HDTransactionData transactionData in conn.GetSpendableOutputs(account.WalletId, account.AccountIndex, currentChainHeight, this.Network.Consensus.CoinbaseMaturity, confirmations))
            {
                var pubKeyScript = new Script(Encoders.Hex.DecodeData(transactionData.ScriptPubKey));
                PubKey pubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(pubKeyScript);

                yield return new UnspentOutputReference()
                {
                    Account = hdAccount,
                    Transaction = this.ToTransactionData(transactionData, HDPayment.GetAllPayments(conn, transactionData.SpendTxTime ?? 0, transactionData.SpendTxId, transactionData.OutputTxId, transactionData.OutputIndex, transactionData.ScriptPubKey)),
                    Confirmations = (currentChainHeight + 1) - transactionData.OutputBlockHeight,
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
        public (Money totalAmount, Money confirmedAmount) GetAccountBalance(WalletAccountReference walletAccountReference, int currentChainHeight, int confirmations = 0)
        {
            DBConnection conn = this.GetConnection(walletAccountReference.WalletName);
            HDAccount account = conn.GetAccountByName(walletAccountReference.WalletName, walletAccountReference.AccountName);

            (decimal total, decimal confirmed) = HDTransactionData.GetBalance(conn, account.WalletId, account.AccountIndex, null, currentChainHeight, (int)this.Network.Consensus.CoinbaseMaturity, confirmations);

            return (new Money(total, MoneyUnit.BTC), new Money(confirmed, MoneyUnit.BTC));
        }

        /// <inheritdoc />
        public IEnumerable<AccountHistory> GetHistory(string walletName, string accountName = null)
        {
            DBConnection conn = this.GetConnection(walletName);
            var accounts = new List<HDAccount>();

            HDWallet wallet = conn.GetWalletByName(walletName);

            if (accountName != null)
            {
                accounts.Add(conn.GetAccountByName(walletName, accountName));
            }
            else
            {
                foreach (HDAccount account in conn.GetAccounts(wallet.WalletId))
                    accounts.Add(account);
            }

            foreach (HDAccount account in accounts)
            {
                var history = new List<FlatHistory>();

                foreach (HDAddress address in conn.GetUsedAddresses(wallet.WalletId, account.AccountIndex, HDAddress.External)
                    .Concat(conn.GetUsedAddresses(wallet.WalletId, account.AccountIndex, HDAddress.Internal)))
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
    }
}
