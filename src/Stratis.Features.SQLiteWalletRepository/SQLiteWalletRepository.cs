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
    internal class WalletContainer
    {
        internal HDWallet Wallet { get; set; }
        internal readonly object lockProcess;
        internal readonly object lockUpdateAccounts;
        internal readonly object lockUpdateAddresses;

        internal WalletContainer(HDWallet wallet)
        {
            this.Wallet = wallet;
            this.lockProcess = new object();
            this.lockUpdateAccounts = new object();
            this.lockUpdateAddresses = new object();
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

        private readonly object lockProcess;
        private readonly object lockUpdateAccounts;
        private readonly object lockUpdateAddresses;

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

            this.lockProcess = new object();
            this.lockUpdateAccounts = new object();
            this.lockUpdateAddresses = new object();
        }

        public void Dispose()
        {
        }

        private DBConnection GetConnection(string walletName = null)
        {
            if (this.DatabasePerWallet)
                Guard.NotNull(walletName, nameof(walletName));

            return new DBConnection(this, this.DatabasePerWallet  ? $"{walletName}.db" : "Wallet.db");
        }

        /// <inheritdoc />
        public void Initialize(bool dbPerWallet = true)
        {
            Directory.CreateDirectory(this.DBPath);

            this.DatabasePerWallet = dbPerWallet;
            if (!dbPerWallet)
            {
                using (DBConnection conn = GetConnection())
                {
                    conn.CreateDBStructure();
                }
            }

            if (this.DatabasePerWallet)
            {
                foreach (string walletName in Directory.EnumerateFiles(this.DBPath, "*.db")
                    .Select(p => p.Substring(this.DBPath.Length + 1).Split('.')[0]))
                {
                    using (DBConnection conn = this.GetConnection())
                    {
                        conn.CreateDBStructure();
                        this.Wallets[walletName] = new WalletContainer(conn.GetWalletByName(walletName));
                    }
                }
            }
            else
            {
                using (DBConnection conn = this.GetConnection())
                {
                    foreach (HDWallet wallet in HDWallet.GetAll(conn))
                    {
                        this.Wallets[wallet.Name] = new WalletContainer(wallet);
                    }
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
            WalletContainer walletContainer = (walletName == null) ? null : this.Wallets[walletName];

            lock (walletContainer?.lockProcess ?? this.lockProcess)
            {
                HDWallet wallet = walletContainer.Wallet;

                using (DBConnection conn = this.GetConnection(walletName))
                {
                    // Perform a sanity check that the location being set is conceivably "within" the wallet.
                    if (!wallet.WalletContainsBlock(lastBlockSynced))
                        throw new InvalidProgramException("Can't rewind the wallet using the supplied tip.");

                    // Ok seems safe. Adjust the tip and rewind relevant transactions.
                    conn.BeginTransaction();
                    conn.SetLastBlockSynced(walletName, lastBlockSynced);
                    conn.Commit();
                }
            }
        }

        public void CreateWallet(string walletName, string encryptedSeed, byte[] chainCode, ChainedHeader lastBlockSynced = null)
        {
            int creationTime;

            if (lastBlockSynced == null)
            {
                creationTime = (int)this.Network.GenesisTime;
            }
            else
            {
                creationTime = (int)lastBlockSynced.Header.Time + 1;
            }

            var wallet = new HDWallet()
            {
                Name = walletName,
                EncryptedSeed = encryptedSeed,
                ChainCode = Convert.ToBase64String(chainCode),
                CreationTime = creationTime
            };

            wallet.SetLastBlockSynced(lastBlockSynced);

            using (var conn = this.GetConnection(walletName))
            {
                conn.BeginTransaction();

                if (this.DatabasePerWallet)
                    conn.CreateDBStructure();

                conn.InsertOrReplace(wallet);
                conn.Commit();

                this.Wallets[wallet.Name] = new WalletContainer(wallet);
            }
        }

        /// <inheritdoc />
        public void CreateAccount(string walletName, int accountIndex, string accountName, ExtPubKey extPubKey, DateTimeOffset? creationTime = null)
        {
            WalletContainer walletContainer = (walletName == null) ? null : this.Wallets[walletName];

            lock (walletContainer?.lockUpdateAccounts ?? this.lockUpdateAccounts)
            {
                HDWallet wallet = walletContainer.Wallet;

                using (var conn = this.GetConnection(walletName))
                {
                    conn.BeginTransaction();

                    var account = conn.CreateAccount(wallet.WalletId, accountIndex, accountName, extPubKey.ToString(this.Network), (int)(creationTime ?? this.DateTimeProvider.GetTimeOffset()).ToUnixTimeSeconds());
                    conn.CreateAddresses(account, 0, 20);
                    conn.CreateAddresses(account, 1, 20);

                    conn.Commit();
                }
            }
        }

        /// <inheritdoc />
        public void CreateAccount(string walletName, int accountIndex, string accountName, string password, DateTimeOffset? creationTime = null)
        {
            WalletContainer walletContainer = (walletName == null) ? null : this.Wallets[walletName];

            lock (walletContainer?.lockUpdateAccounts ?? this.lockUpdateAccounts)
            {
                HDWallet wallet = walletContainer.Wallet;

                ExtPubKey extPubKey;

                using (var conn = this.GetConnection(walletName))
                {
                    // Get the extended pub key used to generate addresses for this account.
                    // Not passing extPubKey into the method to guarantee DB integrity.
                    Key privateKey = Key.Parse(wallet.EncryptedSeed, password, this.Network);
                    var seedExtKey = new ExtKey(privateKey, Convert.FromBase64String(wallet.ChainCode));
                    ExtKey addressExtKey = seedExtKey.Derive(new KeyPath(this.ToHdPath(accountIndex)));
                    extPubKey = addressExtKey.Neuter();
                }

                this.CreateAccount(walletName, accountIndex, accountName, extPubKey, creationTime);
            }
        }

        public IEnumerable<HdAccount> GetAccounts(string walletName)
        {
            using (var conn = this.GetConnection(walletName))
            {
                HDWallet wallet = conn.GetWalletByName(walletName);

                foreach (HDAccount account in conn.GetAccounts(wallet.WalletId))
                    yield return this.ToHdAccount(account);
            }
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isChange = false)
        {
            using (var conn = this.GetConnection(accountReference.WalletName))
            {
                var account = conn.GetAccountByName(accountReference.WalletName, accountReference.AccountName);

                return conn.GetUnusedAddresses(account.WalletId, account.AccountIndex, isChange ? 1 : 0, count).Select(a => this.ToHdAddress(a));
            }
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block, ChainedHeader header, string walletName = null)
        {
            ProcessBlocks(new[] { (header, block) }, walletName);
        }

        /// <inheritdoc />
        public void ProcessBlocks(IEnumerable<(ChainedHeader header, Block block)> blocks, string walletName = null)
        {
            WalletContainer walletContainer = (walletName == null) ? null : this.Wallets[walletName];

            lock (walletContainer?.lockProcess ?? this.lockProcess)
            {
                HDWallet wallet = walletContainer?.Wallet;

                using (DBConnection conn = this.GetConnection(walletName))
                {
                    ChainedHeader deferredTip = null;
                    ChainedHeader deferredSince = null;

                    var addressesOfInterest = new AddressesOfInterest(conn, wallet?.WalletId);
                    var transactionsOfInterest = new TransactionsOfInterest(conn, wallet?.WalletId);

                    addressesOfInterest.AddAll();
                    transactionsOfInterest.AddAll();

                    foreach ((ChainedHeader header, Block block) in blocks.Append((null, null)))
                    {
                        if (block == null)
                        {
                            if (deferredTip != null)
                            {
                                conn.BeginTransaction();
                                HDWallet.AdvanceTip(conn, wallet, deferredTip, deferredSince);
                                conn.Commit();
                            }

                            break;
                        }

                        // Determine the scripts for creating temporary tables and inserting the block's information into them.
                        IEnumerable<IEnumerable<string>> blockToScript;
                        {
                            var lists = TransactionsToLists(block.Transactions, header, null, addressesOfInterest, transactionsOfInterest).ToList();
                            if (lists.Count == 0)
                            {
                                // No work to do.
                                if (deferredTip == null)
                                    deferredSince = header.Previous;
                                deferredTip = header;
                                continue;
                            }

                            blockToScript = lists.Select(list => list.CreateScript());
                        }

                        long flagFall = DateTime.Now.Ticks;

                        // If we're going to process the block then do it with an up-to-date tip.
                        if (deferredTip != null)
                        {
                            conn.BeginTransaction();
                            HDWallet.AdvanceTip(conn, wallet, deferredTip, deferredSince);
                            conn.Commit();

                            deferredTip = null;
                        }

                        conn.BeginTransaction();

                        // Execute the scripts providing the temporary tables to merge with the wallet tables.
                        foreach (IEnumerable<string> tableScript in blockToScript)
                            foreach (string command in tableScript)
                                conn.Execute(command);

                        conn.ProcessTransactions(header, wallet, addressesOfInterest);
                        conn.Commit();

                        addressesOfInterest.Confirm();
                        transactionsOfInterest.Confirm();

                        this.ProcessTime += (DateTime.Now.Ticks - flagFall);
                        this.ProcessCount++;
                    }
                }
            }
        }

        /// <inheritdoc />
        public void RemoveUnconfirmedTransaction(string walletName, uint256 txId)
        {
            using (DBConnection conn = this.GetConnection(walletName))
            {
                HDWallet wallet = conn.GetWalletByName(walletName);

                conn.BeginTransaction();
                conn.RemoveUnconfirmedTransaction(wallet.WalletId, txId);
                conn.Commit();
            }
        }

        /// <inheritdoc />
        public void ProcessTransaction(string walletName, Transaction transaction, uint256 fixedTxId = null)
        {
            WalletContainer walletContainer = (walletName == null) ? null : this.Wallets[walletName];

            lock (walletContainer?.lockProcess ?? this.lockProcess)
            {
                HDWallet wallet = walletContainer.Wallet;

                // TODO: Check that this transaction does not spend UTXO's of any confirmed transactions.

                IEnumerable<IEnumerable<string>> txToScript;
                {
                    var lists = TransactionsToLists(new[] { transaction }, null, fixedTxId).ToList();
                    if (lists.Count == 0)
                        return;
                    txToScript = lists.Select(list => list.CreateScript());
                }

                using (DBConnection conn = this.GetConnection(walletName))
                {
                    // Execute the scripts.
                    foreach (IEnumerable<string> tableScript in txToScript)
                        foreach (string command in tableScript)
                            conn.Execute(command);

                    conn.BeginTransaction();
                    conn.ProcessTransactions(null, wallet);
                    conn.Commit();
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference walletAccountReference, ChainedHeader chainTip, int confirmations = 0)
        {
            using (DBConnection conn = this.GetConnection(walletAccountReference.WalletName))
            {
                HDAccount account = conn.GetAccountByName(walletAccountReference.WalletName, walletAccountReference.AccountName);

                var hdAccount = this.ToHdAccount(account);

                foreach (HDTransactionData transactionData in conn.GetSpendableOutputs(account.WalletId, account.AccountIndex, chainTip.Height, this.Network.Consensus.CoinbaseMaturity, confirmations))
                {
                    var pubKeyScript = new Script(Encoders.Hex.DecodeData(transactionData.ScriptPubKey));
                    PubKey pubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(pubKeyScript);

                    yield return new UnspentOutputReference()
                    {
                        Account = hdAccount,
                        Transaction = this.ToTransactionData(transactionData, HDPayment.GetAllPayments(conn, transactionData.OutputTxTime, transactionData.OutputTxId, transactionData.OutputIndex)),
                        Confirmations = (chainTip.Height + 1) - transactionData.OutputBlockHeight,
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
        }

        /// <inheritdoc />
        public IEnumerable<AccountHistory> GetHistory(string walletName, string accountName = null)
        {
            using (DBConnection conn = this.GetConnection(walletName))
            {
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
        }

        private IEnumerable<TempTable> TransactionsToLists(IEnumerable<Transaction> transactions, ChainedHeader header, uint256 fixedTxId = null, AddressesOfInterest addressesOfInterest = null, TransactionsOfInterest transactionsOfInterest = null)
        {
            // Convert relevant information in the block to information that can be joined to the wallet tables.
            TempTable outputs = null;
            TempTable prevOuts = null;

            foreach (Transaction tx in transactions)
            {
                // Build temp.PrevOuts
                uint256 txId = fixedTxId ?? tx.GetHash();

                foreach (TxIn txIn in tx.Inputs)
                {
                    if (transactionsOfInterest?.Contains(txIn.PrevOut.Hash) ?? true)
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

                    bool unconditional = transactionsOfInterest?.Contains(txId) ?? true; // Related to spending details.

                    foreach (Script pubKeyScript in this.GetDestinations(txOut.ScriptPubKey))
                    {
                        if (unconditional || (addressesOfInterest?.Contains(pubKeyScript) ?? true)) // Paying to one of our addresses.
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
