using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Features.SQLiteWalletRepository.Commands;
using Stratis.Features.SQLiteWalletRepository.External;
using Stratis.Features.SQLiteWalletRepository.Tables;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Interfaces;
using Script = NBitcoin.Script;

[assembly: InternalsVisibleTo("Stratis.Features.SQLiteWalletRepository.Tests")]

namespace Stratis.Features.SQLiteWalletRepository
{
    public class TransactionContext : ITransactionContext
    {
        private int transactionDepth;
        private readonly DBConnection conn;

        public TransactionContext(DBConnection conn)
        {
            this.conn = conn;
            this.transactionDepth = conn.TransactionDepth;
        }

        public void Rollback()
        {
            while (this.conn.IsInTransaction)
            {
                this.conn.Rollback();
            }
        }

        public void Commit()
        {
            while (this.conn.IsInTransaction)
            {
                this.conn.Commit();
            }
        }

        public void Dispose()
        {
            while (this.conn.IsInTransaction)
            {
                this.conn.Rollback();
            }
        }
    }

    internal class TopUpTracker
    {
        internal int WalletId;
        internal int AccountIndex;
        internal int AddressType;
        internal HDAccount Account;
        internal int AddressCount;
        internal int NextAddressIndex;

        internal TopUpTracker(int walletId, int accountIndex, int addressType)
        {
            this.WalletId = walletId;
            this.AccountIndex = accountIndex;
            this.AddressType = addressType;
        }

        internal void ReadAccount(DBConnection conn)
        {
            this.Account = HDAccount.GetAccount(conn, this.WalletId, this.AccountIndex);
            this.AddressCount = HDAddress.GetAddressCount(conn, this.WalletId, this.AccountIndex, this.AddressType);
            this.NextAddressIndex = HDAddress.GetNextAddressIndex(conn, this.WalletId, this.AccountIndex, this.AddressType);
        }

        public override int GetHashCode()
        {
            return (this.WalletId << 8) ^ (this.AccountIndex << 4) ^ this.AddressType;
        }

        public override bool Equals(object obj)
        {
            return (obj as TopUpTracker).WalletId == this.WalletId &&
                   (obj as TopUpTracker).AccountIndex == this.AccountIndex &&
                   (obj as TopUpTracker).AddressType == this.AddressType;
        }
    }

    internal class DBLock
    {
        private readonly SemaphoreSlim slimLock;
        private Dictionary<int, int> depths;

        public DBLock()
        {
            this.slimLock = new SemaphoreSlim(1, 1);
            this.depths = new Dictionary<int, int>(); ;
        }

        public void Release()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            if (this.depths.TryGetValue(threadId, out int depth))
            {
                if (depth > 0)
                {
                    this.depths[threadId] = depth - 1;
                    return;
                }

                this.depths.Remove(threadId);
            }

            this.slimLock.Release();
        }

        public bool Wait(int millisecondsTimeout = int.MaxValue)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            if (this.depths.TryGetValue(threadId, out int depth))
            {
                this.depths[threadId] = depth + 1;
                return true;
            }

            if (!this.slimLock.Wait(millisecondsTimeout))
                return false;

            this.depths[threadId] = 0;

            return true;
        }
    }

    internal class ProcessBlocksInfo
    {
        internal TempTable Outputs;
        internal TempTable PrevOuts;
        internal AddressesOfInterest AddressesOfInterest;
        internal TransactionsOfInterest TransactionsOfInterest;
        internal ChainedHeader NewTip;
        internal ChainedHeader PrevTip;
        internal bool MustCommit;
        internal DBConnection Conn;
        internal HDWallet Wallet;
        internal long NextScheduledCatchup;

        internal DBLock LockProcessBlocks;

        internal ProcessBlocksInfo(DBConnection conn, ProcessBlocksInfo processBlocksInfo, HDWallet wallet = null)
        {
            this.NewTip = null;
            this.PrevTip = null;
            this.MustCommit = false;
            this.Conn = conn;
            this.Wallet = wallet;
            this.LockProcessBlocks = processBlocksInfo?.LockProcessBlocks ?? new DBLock();
            this.Outputs = TempTable.Create<TempOutput>();
            this.PrevOuts = TempTable.Create<TempPrevOut>();

            this.AddressesOfInterest = processBlocksInfo?.AddressesOfInterest ?? new AddressesOfInterest(conn, wallet?.WalletId);
            this.TransactionsOfInterest = processBlocksInfo?.TransactionsOfInterest ?? new TransactionsOfInterest(conn, wallet?.WalletId);
        }
    }

    internal class WalletContainer : ProcessBlocksInfo
    {
        internal readonly DBLock LockUpdateWallet;
        internal readonly DBLock LockUpdateAccounts;
        internal readonly DBLock LockUpdateAddresses;

        internal WalletContainer(DBConnection conn, HDWallet wallet, ProcessBlocksInfo processBlocksInfo = null) : base(conn, processBlocksInfo, wallet)
        {
            this.LockUpdateWallet = new DBLock();
            this.LockUpdateAccounts = new DBLock();
            this.LockUpdateAddresses = new DBLock();
            this.Conn = conn;
        }
    }

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
        internal long ProcessTime;
        internal int ProcessCount;

        public SQLiteWalletRepository(ILoggerFactory loggerFactory, DataFolder dataFolder, Network network, IDateTimeProvider dateTimeProvider, IScriptAddressReader scriptAddressReader)
        {
            this.Network = network;
            this.DataFolder = dataFolder;
            this.dateTimeProvider = dateTimeProvider;
            this.ScriptAddressReader = scriptAddressReader;
            this.WriteMetricsToFile = false;

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

            walletContainer.LockProcessBlocks.Wait();

            try
            {
                HDWallet wallet = walletContainer.Wallet;

                return wallet.GetFork(chainTip);
            }
            finally
            {
                walletContainer.LockProcessBlocks.Release();
            }
        }

        /// <inheritdoc />
        public void RewindWallet(string walletName, ChainedHeader lastBlockSynced)
        {
            WalletContainer walletContainer = this.Wallets[walletName];

            walletContainer.LockProcessBlocks.Wait();

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
                walletContainer.LockProcessBlocks.Release();
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

            wallet.SetLastBlockSynced(lastBlockSynced);

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

            this.Wallets[wallet.Name] = walletContainer;
        }

        /// <inheritdoc />
        public bool DeleteWallet(string walletName)
        {
            WalletContainer walletContainer = this.Wallets[walletName];

            walletContainer.LockProcessBlocks.Wait();
            walletContainer.LockUpdateAccounts.Wait();
            walletContainer.LockUpdateAddresses.Wait();

            try
            {
                this.logger.LogDebug("Deleting wallet '{0}'.", walletName);

                // TODO: Delete HDPayments no longer required.
                if (!this.DatabasePerWallet)
                {
                    this.RewindWallet(walletName, null);

                    int walletId = walletContainer.Wallet.WalletId;

                    DBConnection conn = GetConnection(walletName);
                    conn.BeginTransaction();
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

                    return this.Wallets.TryRemove(walletName, out _);
                }
                else
                {
                    DBConnection conn = GetConnection(walletName);
                    conn.Close();
                    File.Delete(Path.Combine(this.DBPath, $"{walletName}.db"));
                    return this.Wallets.TryRemove(walletName, out _);
                }
            }
            finally
            {
                walletContainer.LockUpdateAddresses.Release();
                walletContainer.LockUpdateAccounts.Release();
                walletContainer.LockProcessBlocks.Release();
            }
        }

        /// <inheritdoc />
        public void CreateAccount(string walletName, int accountIndex, string accountName, ExtPubKey extPubKey, DateTimeOffset? creationTime = null)
        {
            WalletContainer walletContainer = this.Wallets[walletName];

            walletContainer.LockUpdateAccounts.Wait();

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
                walletContainer.LockUpdateAccounts.Release();
            }
        }

        internal void AddAddresses(string walletName, string accountName, int addressType, List<Script> addresses)
        {
            WalletContainer walletContainer = this.Wallets[walletName];

            walletContainer.LockUpdateAccounts.Wait();

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
                walletContainer.LockUpdateAccounts.Release();
            }
        }

        /// <inheritdoc />
        public void CreateAccount(string walletName, int accountIndex, string accountName, string password, DateTimeOffset? creationTime = null)
        {
            WalletContainer walletContainer = this.Wallets[walletName];

            lock (walletContainer.LockUpdateAccounts)
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
                IEnumerable<ProcessBlocksInfo> rounds = this.Wallets.Values;

                foreach ((ChainedHeader header, Block block) in blocks.Append((null, null)))
                {
                    Parallel.ForEach(rounds, round =>
                    {
                        ParallelProcessBlock(round, block, header);
                    });
                }
            }
            else
            {
                ProcessBlocksInfo round = (walletName != null) ? this.Wallets[walletName] : this.processBlocksInfo;

                foreach ((ChainedHeader header, Block block) in blocks.Append((null, null)))
                {
                    ParallelProcessBlock(round, block, header);
                }
            }
        }

        private void ParallelProcessBlock(ProcessBlocksInfo round, Block block, ChainedHeader header)
        {
            if (!round.LockProcessBlocks.Wait(100))
                return;

            try
            {
                HDWallet wallet = round.Wallet;
                DBConnection conn = round.Conn;

                // Flush when new wallets are joining. This ensures that PrevTip will match all wallets requiring updating and advancing.
                string lastBlockSyncedHash = (header?.Previous?.HashBlock ?? (uint256)0).ToString();
                bool walletsJoining;
                if (round.Wallet == null && !this.DatabasePerWallet)
                    walletsJoining = this.Wallets.Any(c => c.Value.Wallet.LastBlockSyncedHash == lastBlockSyncedHash);
                else
                    walletsJoining = round.Wallet.LastBlockSyncedHash == lastBlockSyncedHash;

                if (((round.Outputs.Count + round.PrevOuts.Count) >= 10000) || block == null || walletsJoining)
                {
                    if (round.Outputs.Count != 0 || round.PrevOuts.Count != 0)
                    {
                        IEnumerable<IEnumerable<string>> blockToScript = (new[] { round.Outputs, round.PrevOuts }).Select(list => list.CreateScript());

                        long flagFall = DateTime.Now.Ticks;

                        if (!round.MustCommit && !conn.IsInTransaction)
                        {
                            conn.BeginTransaction();
                            round.MustCommit = true;
                        }

                        conn.ProcessTransactions(blockToScript, wallet, round.NewTip, round.PrevTip, round.AddressesOfInterest);

                        round.Outputs.Clear();
                        round.PrevOuts.Clear();

                        round.AddressesOfInterest.Confirm();
                        round.TransactionsOfInterest.Confirm();

                        this.ProcessTime += (DateTime.Now.Ticks - flagFall);
                    }
                    else
                    {
                        if (round.NewTip != null)
                            HDWallet.AdvanceTip(conn, wallet, round.NewTip, round.PrevTip);
                    }

                    if (round.MustCommit)
                    {
                        conn.Commit();
                        round.MustCommit = false;
                    }

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

                    LogMetrics(conn, header, wallet);
                }

                if (block == null)
                    return;

                // Determine the scripts for creating temporary tables and inserting the block's information into them.
                bool wasInTransaction = conn.IsInTransaction;
                if (TransactionsToLists(conn, block.Transactions, header, null, round))
                    this.ProcessCount++;
                if (conn.IsInTransaction && !wasInTransaction)
                    round.MustCommit = true;

                round.NewTip = header;
                if (round.PrevTip == null)
                    round.PrevTip = header.Previous;
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
            finally
            {
                round.LockProcessBlocks.Release();
            }
        }

        private void LogMetrics(DBConnection conn, ChainedHeader header, HDWallet wallet)
        {
            // Write some metrics to file.
            if (this.WriteMetricsToFile)
            {
                string fixedWidth(object val, int width)
                {
                    return string.Format($"{{0,{width}}}", val);
                }

                var lines = new List<string>();
                lines.Add($"--- Date/Time: {(DateTime.Now.ToString())}, Block Height: { (header?.Height) } ---");

                foreach ((string cmdName, DBCommand cmd) in conn.Commands.Select(kv => (kv.Key, kv.Value)))
                {
                    var key = fixedWidth(cmdName, -20);
                    var time = fixedWidth(((double)cmd.ProcessTime / 10_000_000).ToString("N06"), 8);
                    var count = fixedWidth(cmd.ProcessCount, 5);
                    var avgsec = (cmd.ProcessCount == 0) ? null : fixedWidth(((double)cmd.ProcessTime / cmd.ProcessCount / 10_000_000).ToString("N06"), 8);

                    lines.Add($"{key}: Time={time}, Count={count}, AvgSec={avgsec}");
                }

                lines.Add("");

                foreach (var kv in conn.Commands)
                {
                    kv.Value.ProcessCount = 0;
                    kv.Value.ProcessTime = 0;
                }

                if (wallet != null)
                    File.AppendAllLines(Path.Combine(this.DBPath, $"Metrics_{ wallet.Name }.txt"), lines);
                else
                    File.AppendAllLines(Path.Combine(this.DBPath, "Metrics.txt"), lines);
            }
        }

        /// <inheritdoc />
        public void RemoveUnconfirmedTransaction(string walletName, uint256 txId)
        {
            DBConnection conn = this.GetConnection(walletName);
            HDWallet wallet = conn.GetWalletByName(walletName);
            conn.BeginTransaction();
            conn.RemoveUnconfirmedTransaction(wallet.WalletId, txId);
            conn.Commit();
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
                IEnumerable<IEnumerable<string>> txToScript;
                {
                    var processBlocksInfo = new ProcessBlocksInfo(conn, walletContainer, wallet);
                    TransactionsToLists(conn, new[] { transaction }, null, fixedTxId, processBlocksInfo: processBlocksInfo);
                    txToScript = (new[] { processBlocksInfo.Outputs, processBlocksInfo.PrevOuts }).Select(list => list.CreateScript());
                }

                conn.BeginTransaction();
                conn.ProcessTransactions(txToScript, wallet);
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

        private bool TransactionsToLists(DBConnection conn, IEnumerable<Transaction> transactions, ChainedHeader header, uint256 fixedTxId = null, ProcessBlocksInfo processBlocksInfo = null)
        {
            bool additions = false;

            // Convert relevant information in the block to information that can be joined to the wallet tables.
            TransactionsOfInterest transactionsOfInterest = processBlocksInfo.TransactionsOfInterest;
            AddressesOfInterest addressesOfInterest = processBlocksInfo.AddressesOfInterest;

            // Used for tracking address top-up requirements.
            var trackers = new Dictionary<TopUpTracker, TopUpTracker>();

            foreach (Transaction tx in transactions)
            {
                // Build temp.PrevOuts
                uint256 txId = fixedTxId ?? tx.GetHash();
                bool addSpendTx = false;

                for (int i = 0; i < tx.Inputs.Count; i++)
                {
                    TxIn txIn = tx.Inputs[i];

                    if (transactionsOfInterest?.Contains(txIn.PrevOut) ?? true)
                    {
                        // Record our outputs that are being spent.
                        processBlocksInfo.PrevOuts.Add(new TempPrevOut()
                        {
                            OutputTxId = txIn.PrevOut.Hash.ToString(),
                            OutputIndex = (int)txIn.PrevOut.N,
                            SpendBlockHeight = header?.Height ?? 0,
                            SpendBlockHash = header?.HashBlock.ToString(),
                            SpendTxIsCoinBase = (tx.IsCoinBase || tx.IsCoinStake) ? 1 : 0,
                            SpendTxTime = (int)tx.Time,
                            SpendTxId = txId.ToString(),
                            SpendIndex = i,
                            SpendTxTotalOut = tx.TotalOut.ToDecimal(MoneyUnit.BTC)
                        });

                        additions = true;
                        addSpendTx = true;
                    }
                }

                // Build temp.Outputs.
                for (int i = 0; i < tx.Outputs.Count; i++)
                {
                    TxOut txOut = tx.Outputs[i];

                    if (txOut.IsEmpty)
                        continue;

                    if (txOut.ScriptPubKey.ToBytes(true)[0] == (byte)OpcodeType.OP_RETURN)
                        continue;

                    foreach (Script pubKeyScript in this.GetDestinations(txOut.ScriptPubKey))
                    {
                        bool containsAddress = addressesOfInterest.Contains(pubKeyScript, out HDAddress address);

                        // Paying to one of our addresses?
                        if (addSpendTx || containsAddress)
                        {
                            // Check if top-up is required.
                            if (containsAddress && address != null)
                            {
                                // Get the top-up tracker that applies to this account and address type.
                                var key = new TopUpTracker(address.WalletId, address.AccountIndex, address.AddressType);
                                if (!trackers.TryGetValue(key, out TopUpTracker tracker))
                                {
                                    tracker = key;
                                    tracker.ReadAccount(conn);
                                    trackers.Add(tracker, tracker);
                                }

                                // If an address inside the address buffer is being used then top-up the buffer.
                                while (address.AddressIndex >= tracker.NextAddressIndex)
                                {
                                    HDAddress newAddress = conn.CreateAddress(tracker.Account, tracker.AddressType, tracker.AddressCount);

                                    if (!conn.IsInTransaction)
                                    {
                                        // We've postponed creating a transaction since we weren't sure we will need it.
                                        // Create it now.
                                        conn.BeginTransaction();
                                    }

                                    // Insert the new address into the database.
                                    conn.Insert(newAddress);

                                    // Add the new address to our addresses of interest.
                                    addressesOfInterest.AddTentative(Script.FromHex(newAddress.ScriptPubKey));
                                    addressesOfInterest.Confirm();

                                    // Update the information in the tracker.
                                    tracker.NextAddressIndex++;
                                    tracker.AddressCount++;
                                }
                            }

                            // Record outputs received by our wallets.
                            processBlocksInfo.Outputs.Add(new TempOutput()
                            {
                                // For matching HDAddress.ScriptPubKey.
                                ScriptPubKey = pubKeyScript.ToHex(),

                                // The ScriptPubKey from the txOut.
                                RedeemScript = txOut.ScriptPubKey.ToHex(),

                                OutputBlockHeight = header?.Height ?? 0,
                                OutputBlockHash = header?.HashBlock.ToString(),
                                OutputTxIsCoinBase = (tx.IsCoinBase || tx.IsCoinStake) ? 1 : 0,
                                OutputTxTime = (int)tx.Time,
                                OutputTxId = txId.ToString(),
                                OutputIndex = i,
                                Value = txOut.Value.ToDecimal(MoneyUnit.BTC)
                            });

                            additions = true;

                            if (containsAddress)
                                transactionsOfInterest?.AddTentative(new OutPoint(txId, i));
                        }
                    }
                }
            }

            return additions;
        }
    }
}
