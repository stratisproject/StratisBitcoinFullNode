using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Features.SQLiteWalletRepository.Tables;
using NBitcoin.DataEncoders;
using Stratis.Features.SQLiteWalletRepository.Extensions;

namespace Stratis.Features.SQLiteWalletRepository
{
    public class SQLiteWalletRepository : LockProtected, IWalletRepository, IDisposable
    {
        internal Network Network { get; private set; }
        internal DataFolder DataFolder { get; private set; }
        internal IDateTimeProvider DateTimeProvider { get; private set; }
        internal IScriptPubKeyProvider ScriptPubKeyProvider { get; private set; }

        internal string DBPath
        {
            get
            {
                return Path.Combine(this.DataFolder.WalletPath, nameof(SQLiteWalletRepository));
            }
        }

        internal string DBFile
        {
            get
            {
                return Path.Combine(this.DBPath, "Wallets.db");
            }
        }

        public SQLiteWalletRepository(DataFolder dataFolder, Network network, IDateTimeProvider dateTimeProvider, IScriptPubKeyProvider scriptPubKeyProvider)
        {
            this.Network = network;
            this.DataFolder = dataFolder;
            this.DateTimeProvider = dateTimeProvider;
            this.ScriptPubKeyProvider = scriptPubKeyProvider;
        }

        public void Dispose()
        {
        }

        private DBConnection GetConnection()
        {
            return new DBConnection(this);
        }

        /// <inheritdoc />
        public void Initialize(bool wipeDB = false)
        {
            lock (this.lockObject)
            {
                Directory.CreateDirectory(this.DBPath);

                if (wipeDB && File.Exists(this.DBFile))
                    File.Delete(this.DBFile);

                using (var conn = GetConnection())
                {
                    conn.BeginTransaction();
                    conn.CreateTable<HDWallet>();
                    conn.CreateTable<HDAccount>();
                    conn.CreateTable<HDAddress>();
                    conn.CreateTable<HDTransactionData>();
                    conn.CreateTable<HDPayment>();
                    conn.Commit();
                }
            }
        }

        /// <inheritdoc />
        public void SetLastBlockSynced(string walletName, ChainedHeader lastBlockSynced)
        {
            lock (this.lockObject)
            {
                using (DBConnection conn = this.GetConnection())
                {
                    if (lastBlockSynced != null)
                    {
                        // Perform a sanity check that the location being set is conceivably "within" the wallet.
                        HDWallet wallet = conn.GetWalletByName(walletName);

                        if (lastBlockSynced.Height > wallet.LastBlockSyncedHeight)
                            throw new InvalidProgramException("Can't rewind the wallet using the supplied tip.");

                        if (lastBlockSynced.Height == wallet.LastBlockSyncedHeight)
                        {
                            if (lastBlockSynced.HashBlock == uint256.Parse(wallet.LastBlockSyncedHash))
                                return;

                            throw new InvalidProgramException("Can't rewind the wallet using the supplied tip.");
                        }

                        var blockLocator = new BlockLocator()
                        {
                            Blocks = wallet.BlockLocator.Split(',').Select(strHash => uint256.Parse(strHash)).ToList()
                        };

                        List<int> locatorHeights = ChainedHeaderExt.GetLocatorHeights(wallet.LastBlockSyncedHeight);

                        for (int i = 0; i < locatorHeights.Count; i++)
                        {
                            if (lastBlockSynced.Height >= locatorHeights[i])
                            {
                                lastBlockSynced = lastBlockSynced.GetAncestor(locatorHeights[i]);

                                if (lastBlockSynced.HashBlock != blockLocator.Blocks[i])
                                    throw new InvalidProgramException("Can't rewind the wallet using the supplied tip.");

                                break;
                            }
                        }
                    }

                    // Ok seems safe. Adjust the tip and rewind relevant transactions.
                    conn.BeginTransaction();
                    conn.SetLastBlockSynced(walletName, lastBlockSynced);
                    conn.Commit();
                }
            }
        }

        public void CreateWallet(string walletName, string encryptedSeed, byte[] chainCode, ChainedHeader lastBlockSynced = null)
        {
            lock (this.lockObject)
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

                using (var conn = this.GetConnection())
                {
                    conn.BeginTransaction();
                    conn.Insert(wallet);
                    conn.Commit();
                }
            }
        }

        /// <inheritdoc />
        public void CreateAccount(string walletName, int accountIndex, string accountName, string password, string scriptPubKeyType, DateTimeOffset? creationTime = null)
        {
            lock (this.lockObject)
            {
                using (var conn = this.GetConnection())
                {
                    var wallet = conn.GetWalletByName(walletName);

                    // Get the extended pub key used to generate addresses for this account.
                    // Not passing extPubKey into the method to guarantee DB integrity.
                    Key privateKey = Key.Parse(wallet.EncryptedSeed, password, this.Network);
                    var seedExtKey = new ExtKey(privateKey, Convert.FromBase64String(wallet.ChainCode));
                    ExtKey addressExtKey = seedExtKey.Derive(new KeyPath(this.ToHdPath(accountIndex)));
                    ExtPubKey extPubKey = addressExtKey.Neuter();

                    conn.BeginTransaction();

                    var account = conn.CreateAccount(wallet.WalletId, accountIndex, accountName, extPubKey.ToString(this.Network), scriptPubKeyType, (int)(creationTime ?? this.DateTimeProvider.GetTimeOffset()).ToUnixTimeSeconds());
                    if (!string.IsNullOrEmpty(account.ScriptPubKeyType))
                    {
                        conn.CreateAddresses(account, 0, 20);
                        conn.CreateAddresses(account, 1, 20);
                    }

                    conn.Commit();
                }
            }
        }

        public IEnumerable<HdAccount> GetAccounts(string walletName)
        {
            lock (this.lockObject)
            {
                using (var conn = this.GetConnection())
                {
                    HDWallet wallet = conn.GetWalletByName(walletName);

                    foreach (HDAccount account in conn.GetAccounts(wallet.WalletId))
                        yield return this.ToHdAccount(account);
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isChange = false)
        {
            lock (this.lockObject)
            {
                using (var conn = this.GetConnection())
                {
                    var account = conn.GetAccountByName(accountReference.WalletName, accountReference.AccountName);
                    foreach (HDAddress address in conn.GetUnusedAddresses(account.WalletId, account.AccountIndex, isChange ? 1 : 0, count))
                    {
                        yield return this.ToHdAddress(address);
                    }
                }
            }
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block, ChainedHeader header)
        {
            Guard.NotNull(header, nameof(header));

            // TODO: Perhaps return a list of hashes of displaced transient transactions.

            lock (this.lockObject)
            {
                // Determine the scripts for creating temporary tables and inserting the block's information into them.
                IEnumerable<IEnumerable<string>> blockToScript;
                {
                    var lists = TransactionsToLists(block.Transactions, header).ToList();
                    blockToScript = lists.Select(list => list.CreateScript());
                }

                // Merge the temporary tables with the wallet tables.
                using (DBConnection conn = this.GetConnection())
                {
                    // Execute the scripts.
                    foreach (IEnumerable<string> tableScript in blockToScript)
                        foreach (string command in tableScript)
                            conn.Execute(command);

                    conn.BeginTransaction();
                    conn.ProcessTransactions(header);
                    conn.Commit();
                }
            }
        }

        /// <inheritdoc />
        public void RemoveUnconfirmedTransaction(string walletName, uint256 txId)
        {
            lock (this.lockObject)
            {
                using (DBConnection conn = this.GetConnection())
                {
                    HDWallet wallet = conn.GetWalletByName(walletName);

                    conn.RemoveUnconfirmedTransaction(wallet.WalletId, txId);
                }
            }
        }

        /// <inheritdoc />
        public void ProcessTransaction(string walletName, Transaction transaction, uint256 fixedTxId = null)
        {
            Guard.NotNull(walletName, nameof(walletName));

            lock (this.lockObject)
            {
                // TODO: Check that this transaction does not spend UTXO's of any confirmed transactions.

                IEnumerable<IEnumerable<string>> txToScript;
                {
                    var lists = TransactionsToLists(new[] { transaction }, null, fixedTxId).ToList();
                    txToScript = lists.Select(list => list.CreateScript());
                }

                using (DBConnection conn = this.GetConnection())
                {
                    // Execute the scripts.
                    foreach (IEnumerable<string> tableScript in txToScript)
                        foreach (string command in tableScript)
                            conn.Execute(command);

                    conn.BeginTransaction();
                    conn.ProcessTransactions(null, walletName);
                    conn.Commit();
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference walletAccountReference, ChainedHeader chainTip, int confirmations = 0)
        {
            lock (this.lockObject)
            {
                using (DBConnection conn = this.GetConnection())
                {
                    HDAccount account = conn.GetAccountByName(walletAccountReference.WalletName, walletAccountReference.AccountName);

                    var hdAccount = this.ToHdAccount(account);

                    foreach (HDTransactionData transactionData in conn.GetSpendableOutputs(account.WalletId, account.AccountIndex, chainTip.Height, this.Network.Consensus.CoinbaseMaturity))
                    {
                        var pubKeyScript = new Script(Encoders.Hex.DecodeData(transactionData.PubKey));
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
                                 PubKey = transactionData.PubKey,
                                 ScriptPubKey = transactionData.ScriptPubKey
                            })
                        };
                    }
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<AccountHistory> GetHistory(string walletName, string accountName = null)
        {
            lock (this.lockObject)
            {
                using (DBConnection conn = this.GetConnection())
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
        }

        private IEnumerable<TempTable> TransactionsToLists(IEnumerable<Transaction> transactions, ChainedHeader header, uint256 fixedTxId = null)
        {
            // Convert relevant information in the block to information that can be joined to the wallet tables.
            var outputs = TempTable.Create<TempOutput>();
            var prevOuts = TempTable.Create<TempPrevOut>();

            foreach (Transaction tx in transactions)
            {
                // Build temp.PrevOuts
                foreach (TxIn txIn in tx.Inputs)
                {
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
                        SpendTxId = tx.GetHash().ToString(),
                        SpendTxTotalOut = tx.TotalOut.ToDecimal(MoneyUnit.BTC)
                    });
                }

                // Build temp.Outputs.
                string txHash = (fixedTxId ?? tx.GetHash()).ToString();

                for (int i = 0; i < tx.Outputs.Count; i++)
                {
                    TxOut txOut = tx.Outputs[i];

                    if (txOut.IsEmpty)
                        continue;

                    if (txOut.ScriptPubKey.ToBytes(true)[0] == (byte)OpcodeType.OP_RETURN)
                        continue;

                    string scriptPubKey = txOut.ScriptPubKey.ToHex();

                    // We don't know which of these are actually received by our
                    // wallet addresses but we records them for batched resolution.
                    outputs.Add(new TempOutput()
                    {
                        ScriptPubKey = scriptPubKey,
                        OutputBlockHeight = header?.Height ?? 0,
                        OutputBlockHash = header?.HashBlock.ToString(),
                        OutputTxIsCoinBase = (tx.IsCoinBase || tx.IsCoinStake) ? 1 : 0,
                        OutputTxTime = (int)tx.Time,
                        OutputTxId = txHash,
                        OutputIndex = i,
                        Value = txOut.Value.ToDecimal(MoneyUnit.BTC)
                    });
                }

                yield return prevOuts;
                yield return outputs;
            }
        }
    }
}
