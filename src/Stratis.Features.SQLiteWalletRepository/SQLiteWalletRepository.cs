using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Features.SQLiteWalletRepository.Tables;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Interfaces;
using Script = NBitcoin.Script;

[assembly: InternalsVisibleTo("Stratis.Features.SQLiteWalletRepository.Tests")]

namespace Stratis.Features.SQLiteWalletRepository
{
    internal class ProcessBlocksInfo
    {
        internal AddressesOfInterest AddressesOfInterest;
        internal TransactionsOfInterest TransactionsOfInterest;
        internal ChainedHeader DeferredTip;
        internal ChainedHeader DeferredSince;
        internal DBConnection Conn;
        internal HDWallet Wallet;
        internal object LockProcessBlocks;

        internal ProcessBlocksInfo(DBConnection conn, ProcessBlocksInfo processBlocksInfo, HDWallet wallet = null)
        {
            this.DeferredTip = null;
            this.DeferredSince = null;
            this.Conn = null;
            this.Wallet = wallet;
            this.LockProcessBlocks = processBlocksInfo?.LockProcessBlocks ?? new object();

            this.AddressesOfInterest = processBlocksInfo?.AddressesOfInterest ?? new AddressesOfInterest();
            this.TransactionsOfInterest = processBlocksInfo?.TransactionsOfInterest ?? new TransactionsOfInterest();
            this.AddressesOfInterest.AddAll(conn, wallet?.WalletId);
            this.TransactionsOfInterest.AddAll(conn, wallet?.WalletId);
        }
    }

    internal class WalletContainer : ProcessBlocksInfo
    {
        internal readonly object LockUpdateAccounts;
        internal readonly object LockUpdateAddresses;

        internal WalletContainer(DBConnection conn, HDWallet wallet, ProcessBlocksInfo processBlocksInfo = null) : base(conn, processBlocksInfo, wallet)
        {
            this.LockUpdateAccounts = new object();
            this.LockUpdateAddresses = new object();
            this.Conn = conn;
        }
    }

    public class SQLiteWalletRepository : LockProtected, IWalletRepository, IDisposable
    {
        internal Network Network { get; private set; }
        internal DataFolder DataFolder { get; private set; }
        internal IDateTimeProvider DateTimeProvider { get; private set; }
        internal IScriptAddressReader ScriptAddressReader { get; private set; }

        internal long ProcessTime;
        internal int ProcessCount;

        public Dictionary<string, long> Metrics = new Dictionary<string, long>();

        public bool DatabasePerWallet { get; private set; }

        private ProcessBlocksInfo processBlocksInfo;

        internal ConcurrentDictionary<string, WalletContainer> Wallets;

        internal string DBPath
        {
            get
            {
                return Path.Combine(this.DataFolder.WalletPath, nameof(SQLiteWalletRepository));
            }
        }

        public SQLiteWalletRepository(DataFolder dataFolder, Network network, IDateTimeProvider dateTimeProvider, IScriptAddressReader scriptAddressReader)
        {
            this.Network = network;
            this.DataFolder = dataFolder;
            this.DateTimeProvider = dateTimeProvider;
            this.ScriptAddressReader = scriptAddressReader;

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
            else if (this.Wallets.Count > 0)
                return this.Wallets.First().Value.Conn;

            if (walletName != null && this.Wallets.ContainsKey(walletName))
                return this.Wallets[walletName].Conn;

            var conn = new DBConnection(this, this.DatabasePerWallet ? $"{walletName}.db" : "Wallet.db");
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

            if (this.DatabasePerWallet)
            {
                foreach (string walletName in Directory.EnumerateFiles(this.DBPath, "*.db")
                    .Select(p => p.Substring(this.DBPath.Length + 1).Split('.')[0]))
                {
                    var conn = GetConnection(walletName);

                    HDWallet wallet = conn.GetWalletByName(walletName);
                    var walletContainer = new WalletContainer(conn, conn.GetWalletByName(walletName));
                    this.Wallets[walletName] = walletContainer;
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
                }
            }
        }

        public List<string> GetWalletNames()
        {
            return this.Wallets.Select(kv => kv.Value.Wallet.Name).ToList();
        }

        /// <inheritdoc />
        public void RewindWallet(string walletName, ChainedHeader lastBlockSynced)
        {
            WalletContainer walletContainer = this.Wallets[walletName];

            lock (walletContainer.LockProcessBlocks)
            {
                HDWallet wallet = walletContainer.Wallet;

                DBConnection conn = this.GetConnection(walletName);

                // Perform a sanity check that the location being set is conceivably "within" the wallet.
                if (!wallet.WalletContainsBlock(lastBlockSynced))
                    throw new InvalidProgramException("Can't rewind the wallet using the supplied tip.");

                // Ok seems safe. Adjust the tip and rewind relevant transactions.
                conn.BeginTransaction();
                conn.SetLastBlockSynced(walletName, lastBlockSynced);
                conn.Commit();
            }
        }

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
            conn.BeginTransaction();
            wallet.CreateWallet(conn);
            conn.Commit();

            WalletContainer walletContainer;
            if (this.DatabasePerWallet)
                walletContainer = new WalletContainer(conn, wallet);
            else
                walletContainer = new WalletContainer(conn, wallet, this.processBlocksInfo);

            this.Wallets[wallet.Name] = walletContainer;
        }

        /// <inheritdoc />
        public void CreateAccount(string walletName, int accountIndex, string accountName, ExtPubKey extPubKey, DateTimeOffset? creationTime = null)
        {
            WalletContainer walletContainer = this.Wallets[walletName];

            lock (walletContainer.LockUpdateAccounts)
            {
                HDWallet wallet = walletContainer.Wallet;

                var conn = this.GetConnection(walletName);

                conn.BeginTransaction();

                var account = conn.CreateAccount(wallet.WalletId, accountIndex, accountName, extPubKey.ToString(this.Network), (int)(creationTime ?? this.DateTimeProvider.GetTimeOffset()).ToUnixTimeSeconds());
                conn.CreateAddresses(account, 0, 20);
                conn.CreateAddresses(account, 1, 20);

                walletContainer.AddressesOfInterest.AddAll(conn, wallet.WalletId);

                conn.Commit();
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
        public void ProcessBlock(Block block, ChainedHeader header, string walletName = null)
        {
            ProcessBlocks(new[] { (header, block) }, walletName);
        }

        /// <inheritdoc />
        public void ProcessBlocks(IEnumerable<(ChainedHeader header, Block block)> blocks, string walletName = null)
        {
            long nextScheduledCatchup = 0;
            HDWallet wallet = null;
            DBConnection conn = null;
            IEnumerable<ProcessBlocksInfo> rounds;

            if (this.Wallets.Count == 0)
                return;

            if (this.DatabasePerWallet)
            {
                if (walletName != null)
                    rounds = new[] { this.Wallets[walletName] };
                else
                    rounds = this.Wallets.Values;
            }
            else
            {
                conn = this.GetConnection(walletName);

                if (walletName != null)
                    wallet = conn.GetWalletByName(walletName);

                rounds = new[] { this.processBlocksInfo };
            }

            foreach ((ChainedHeader header, Block block) in blocks.Append((null, null)))
            {
                foreach (ProcessBlocksInfo round in rounds)
                {
                    lock (round.LockProcessBlocks)
                    {
                        if (this.DatabasePerWallet)
                        {
                            wallet = (round as WalletContainer).Wallet;
                            conn = (round as WalletContainer).Conn;
                        }

                        void DeferredTipCatchup()
                        {
                            if (round.DeferredTip != null)
                            {
                                try
                                {
                                    conn.BeginTransaction();
                                    HDWallet.AdvanceTip(conn, wallet, round.DeferredTip, round.DeferredSince);
                                    conn.Commit();

                                    round.DeferredTip = null;
                                }
                                catch (Exception)
                                {
                                    conn.Rollback();
                                    throw;
                                }
                            }

                            nextScheduledCatchup = DateTime.Now.Ticks + 10 * 10_000_000;
                        }

                        if (block == null)
                        {
                            DeferredTipCatchup();
                            continue;
                        }

                        // Determine the scripts for creating temporary tables and inserting the block's information into them.
                        IEnumerable<IEnumerable<string>> blockToScript;
                        {
                            var lists = TransactionsToLists(conn, block.Transactions, header, null, round).ToList();
                            if (lists.Count == 0)
                            {
                                // No work to do.
                                if (round.DeferredTip == null)
                                    round.DeferredSince = header.Previous;
                                round.DeferredTip = header;

                                if (DateTime.Now.Ticks >= nextScheduledCatchup)
                                    DeferredTipCatchup();

                                continue;
                            }

                            blockToScript = lists.Select(list => list.CreateScript());
                        }

                        // If we're going to process the block then do it with an up-to-date tip.
                        DeferredTipCatchup();

                        long flagFall = DateTime.Now.Ticks;
                        try
                        {
                            string lastBlockSyncedHash = (header.Previous?.HashBlock ?? uint256.Zero).ToString();

                            // Determine which wallets will be updated.
                            List<HDWallet> updatingWallets = conn.Query<HDWallet>($@"
                                SELECT *
                                FROM   HDWallet
                                WHERE  LastBlockSyncedHash = '{lastBlockSyncedHash}' {
                            // Respect the wallet name if provided.
                            ((walletName != null) ? $@"
                                AND    Name = '{walletName}'" : "")}");

                            conn.BeginTransaction();

                            // Execute the scripts providing the temporary tables to merge with the wallet tables.
                            foreach (IEnumerable<string> tableScript in blockToScript)
                                foreach (string command in tableScript)
                                    conn.Execute(command);

                            conn.ProcessTransactions(header, wallet, round.AddressesOfInterest);
                            conn.Commit();

                            round.AddressesOfInterest.Confirm(conn, wallet?.WalletId);
                            round.TransactionsOfInterest.Confirm(conn, wallet?.WalletId);

                            string blockLocator = string.Join(",", header?.GetLocator().Blocks);

                            foreach (HDWallet updatingWallet in updatingWallets)
                            {
                                updatingWallet.LastBlockSyncedHash = header.HashBlock.ToString();
                                updatingWallet.LastBlockSyncedHeight = header.Height;
                                updatingWallet.BlockLocator = blockLocator;

                                this.Wallets[updatingWallet.Name].Wallet = updatingWallet;
                            }
                        }
                        catch (Exception)
                        {
                            conn.Rollback();
                            throw;
                        }

                        this.ProcessTime += (DateTime.Now.Ticks - flagFall);
                        this.ProcessCount++;
                    }
                }
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

            lock (walletContainer.LockProcessBlocks)
            {
                HDWallet wallet = walletContainer.Wallet;

                // TODO: Check that this transaction does not spend UTXO's of any confirmed transactions.

                DBConnection conn = this.GetConnection(walletName);
                IEnumerable<IEnumerable<string>> txToScript;
                {
                    var lists = TransactionsToLists(conn, new[] { transaction }, null, fixedTxId).ToList();
                    if (lists.Count == 0)
                        return;
                    txToScript = lists.Select(list => list.CreateScript());
                }

                // Execute the scripts.
                foreach (IEnumerable<string> tableScript in txToScript)
                    foreach (string command in tableScript)
                        conn.Execute(command);

                conn.BeginTransaction();
                conn.ProcessTransactions(null, wallet);
                conn.Commit();
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
                    Transaction = this.ToTransactionData(transactionData, HDPayment.GetAllPayments(conn, transactionData.OutputTxTime, transactionData.OutputTxId, transactionData.OutputIndex)),
                    Confirmations = (currentChainHeight + 1) - transactionData.OutputBlockHeight,
                    Address = this.ToHdAddress(new HDAddress()
                    {
                            AccountIndex = transactionData.AccountIndex,
                            AddressIndex = transactionData.AddressIndex,
                            AddressType = transactionData.AddressType,
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

                foreach (HDAddress address in conn.GetUsedAddresses(wallet.WalletId, account.AccountIndex, 0)
                    .Concat(conn.GetUsedAddresses(wallet.WalletId, account.AccountIndex, 1)))
                {
                    HdAddress hdAddress = this.ToHdAddress(address);

                    foreach (var transaction in conn.GetTransactionsForAddress(wallet.WalletId, account.AccountIndex, address.AddressType, address.AddressIndex))
                    {
                        history.Add(new FlatHistory()
                        {
                            Address = hdAddress,
                            Transaction = this.ToTransactionData(transaction, HDPayment.GetAllPayments(conn, transaction.OutputTxTime, transaction.OutputTxId, transaction.OutputIndex))
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

        private IEnumerable<TempTable> TransactionsToLists(DBConnection conn, IEnumerable<Transaction> transactions, ChainedHeader header, uint256 fixedTxId = null, ProcessBlocksInfo processBlocksInfo = null)
        {
            // Convert relevant information in the block to information that can be joined to the wallet tables.
            TempTable outputs = null;
            TempTable prevOuts = null;

            TransactionsOfInterest transactionsOfInterest = processBlocksInfo.TransactionsOfInterest;
            AddressesOfInterest addressesOfInterest = processBlocksInfo.AddressesOfInterest;
            int? walletId = processBlocksInfo.Wallet?.WalletId;

            foreach (Transaction tx in transactions)
            {
                // Build temp.PrevOuts
                uint256 txId = fixedTxId ?? tx.GetHash();

                foreach (TxIn txIn in tx.Inputs)
                {
                    if (transactionsOfInterest?.Contains(txIn.PrevOut.Hash, conn, walletId) ?? true)
                    {
                        if (prevOuts == null)
                            prevOuts = TempTable.Create<TempPrevOut>();

                        // We don't know which of these are actually spending from our
                        // wallet addresses but we record them for batched resolution.
                        prevOuts.Add(new TempPrevOut()
                        {
                            OutputTxId = txIn.PrevOut.Hash.ToString(),
                            OutputIndex = (int)txIn.PrevOut.N,
                            SpendBlockHeight = header?.Height ?? 0,
                            SpendBlockHash = header?.HashBlock.ToString(),
                            SpendTxIsCoinBase = (tx.IsCoinBase || tx.IsCoinStake) ? 1 : 0,
                            SpendTxTime = (int)tx.Time,
                            SpendTxId = txId.ToString(),
                            SpendTxTotalOut = tx.TotalOut.ToDecimal(MoneyUnit.BTC)
                        });

                        transactionsOfInterest?.AddTentative(txId);
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

                    bool unconditional = transactionsOfInterest?.Contains(txId, conn, walletId) ?? true; // Related to spending details.

                    foreach (Script pubKeyScript in this.GetDestinations(txOut.ScriptPubKey))
                    {
                        if (unconditional || (addressesOfInterest?.Contains(pubKeyScript, conn, walletId) ?? true)) // Paying to one of our addresses.
                        {
                            // We don't know which of these are actually received by our
                            // wallet addresses but we records them for batched resolution.
                            if (outputs == null)
                                outputs = TempTable.Create<TempOutput>();

                            outputs.Add(new TempOutput()
                            {
                                ScriptPubKey = pubKeyScript.ToHex(),              // For matching HDAddress.ScriptPubKey.
                                RedeemScript = txOut.ScriptPubKey.ToHex(),  // The ScriptPubKey from the txOut.
                                OutputBlockHeight = header?.Height ?? 0,
                                OutputBlockHash = header?.HashBlock.ToString(),
                                OutputTxIsCoinBase = (tx.IsCoinBase || tx.IsCoinStake) ? 1 : 0,
                                OutputTxTime = (int)tx.Time,
                                OutputTxId = txId.ToString(),
                                OutputIndex = i,
                                Value = txOut.Value.ToDecimal(MoneyUnit.BTC)
                            });

                            transactionsOfInterest?.AddTentative(txId);
                        }
                    }
                }
            }

            if (prevOuts != null || outputs != null)
            {
                yield return prevOuts ?? TempTable.Create<TempPrevOut>();
                yield return outputs ?? TempTable.Create<TempOutput>();
            }
        }
    }
}
