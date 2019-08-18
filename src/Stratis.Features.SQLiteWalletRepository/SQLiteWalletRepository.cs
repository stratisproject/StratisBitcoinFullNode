﻿using System;
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
    public class SQLiteWalletRepository : LockProtected, IWalletRepository, IDisposable
    {
        internal Network Network { get; private set; }
        internal DataFolder DataFolder { get; private set; }
        internal IDateTimeProvider DateTimeProvider { get; private set; }
        internal IScriptAddressReader ScriptAddressReader { get; private set; }

        internal long ProcessTime;
        internal int ProcessCount;

        public bool DatabasePerWallet { get; private set; }

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
            lock (this.lockObject)
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
            }
        }

        public List<string> GetWalletNames()
        {
            lock (this.lockObject)
            {
                if (this.DatabasePerWallet)
                {
                    return Directory.EnumerateFiles(this.DBPath, "*.db")
                        .Select(p => p.Substring(this.DBPath.Length + 1).Split('.')[0])
                        .ToList();
                }
                else
                {
                    using (DBConnection conn = this.GetConnection())
                    {
                        return HDWallet.GetAll(conn)
                            .Select(w => w.Name)
                            .ToList();
                    }
                }
            }
        }

        /// <inheritdoc />
        public void RewindWallet(string walletName, ChainedHeader lastBlockSynced)
        {
            lock (this.lockObject)
            {
                using (DBConnection conn = this.GetConnection(walletName))
                {
                    HDWallet wallet = conn.GetWalletByName(walletName);

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

                using (var conn = this.GetConnection(walletName))
                {
                    conn.BeginTransaction();

                    if (this.DatabasePerWallet)
                        conn.CreateDBStructure();

                    conn.InsertOrReplace(wallet);
                    conn.Commit();
                }
            }
        }

        /// <inheritdoc />
        public void CreateAccount(string walletName, int accountIndex, string accountName, ExtPubKey extPubKey, string scriptPubKeyType, DateTimeOffset? creationTime = null)
        {
            lock (this.lockObject)
            {
                using (var conn = this.GetConnection(walletName))
                {
                    var wallet = conn.GetWalletByName(walletName);

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

        /// <inheritdoc />
        public void CreateAccount(string walletName, int accountIndex, string accountName, string password, string scriptPubKeyType, DateTimeOffset? creationTime = null)
        {
            lock (this.lockObject)
            {
                ExtPubKey extPubKey;

                using (var conn = this.GetConnection(walletName))
                {
                    var wallet = conn.GetWalletByName(walletName);

                    // Get the extended pub key used to generate addresses for this account.
                    // Not passing extPubKey into the method to guarantee DB integrity.
                    Key privateKey = Key.Parse(wallet.EncryptedSeed, password, this.Network);
                    var seedExtKey = new ExtKey(privateKey, Convert.FromBase64String(wallet.ChainCode));
                    ExtKey addressExtKey = seedExtKey.Derive(new KeyPath(this.ToHdPath(accountIndex)));
                    extPubKey = addressExtKey.Neuter();
                }

                this.CreateAccount(walletName, accountIndex, accountName, extPubKey, scriptPubKeyType, creationTime);
            }
        }

        public IEnumerable<HdAccount> GetAccounts(string walletName)
        {
            lock (this.lockObject)
            {
                using (var conn = this.GetConnection(walletName))
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
                using (var conn = this.GetConnection(accountReference.WalletName))
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
        public void ProcessBlock(Block block, ChainedHeader header, string walletName = null)
        {
            ProcessBlocks(new[] { (header, block) }, walletName);
        }

        /// <inheritdoc />
        public void ProcessBlocks(IEnumerable<(ChainedHeader header, Block block)> blocks, string walletName = null)
        {
            using (DBConnection conn = this.GetConnection(walletName))
            {
                ChainedHeader deferredTip = null;
                HDWallet wallet = (walletName == null) ? null : conn.GetWalletByName(walletName);
                var addressesOfInterest = new AddressesOfInterest(conn, wallet.WalletId);
                var transactionsOfInterest = new TransactionsOfInterest(conn, wallet.WalletId);

                addressesOfInterest.AddAll();
                transactionsOfInterest.AddAll();

                foreach ((ChainedHeader header, Block block) in blocks.Append((null, null)))
                {
                    // TODO: Lock wallets individually.
                    // TODO: Can do some work in parallel.

                    lock (this.lockObject)
                    {
                        if (block == null)
                        {
                            if (deferredTip != null)
                            {
                                conn.BeginTransaction();
                                // TODO: Needs work for multi-wallet updates.
                                wallet.SetLastBlockSynced(deferredTip);
                                conn.Update(wallet);
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
                                deferredTip = header;
                                continue;
                            }

                            blockToScript = lists.Select(list => list.CreateScript());
                        }

                        long flagFall = DateTime.Now.Ticks;

                        conn.BeginTransaction();

                        // Execute the scripts providing the temporary tables to merge with the wallet tables.
                        foreach (IEnumerable<string> tableScript in blockToScript)
                            foreach (string command in tableScript)
                                conn.Execute(command);

                        // If we're going to process the block then do it with an up-to-date tip.
                        if (deferredTip != null)
                        {
                            wallet.SetLastBlockSynced(deferredTip);
                            conn.Update(wallet);
                            deferredTip = null;
                        }

                        conn.ProcessTransactions(header, walletName, addressesOfInterest);
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
            lock (this.lockObject)
            {
                using (DBConnection conn = this.GetConnection(walletName))
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
                using (DBConnection conn = this.GetConnection(walletAccountReference.WalletName))
                {
                    HDAccount account = conn.GetAccountByName(walletAccountReference.WalletName, walletAccountReference.AccountName);

                    var hdAccount = this.ToHdAccount(account);

                    foreach (HDTransactionData transactionData in conn.GetSpendableOutputs(account.WalletId, account.AccountIndex, chainTip.Height, this.Network.Consensus.CoinbaseMaturity, confirmations))
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
                                 ScriptPubKey = transactionData.RedeemScript
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
        }

        private IEnumerable<Script> GetDestinations(Script redeemScript)
        {
            // TODO: "GetAddressFromScriptPubKey" should support returning multiple addresses for cold staking.
            string[] addresses = new[] { this.ScriptAddressReader.GetAddressFromScriptPubKey(this.Network, redeemScript) };

            foreach (string base58 in addresses)
            {
                if (base58 != null)
                {
                    KeyId keyId = null;

                    try
                    {
                        byte[] decoded = Encoders.Base58Check.DecodeData(base58);
                        keyId = new KeyId(new uint160(decoded.Skip(this.Network.GetVersionBytes(Base58Type.PUBKEY_ADDRESS, true).Length).ToArray()));
                    }
                    catch (Exception)
                    {
                        // TODO: Add logging.
                    }

                    if (keyId != null)
                        yield return keyId.ScriptPubKey;
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
                    if (transactionsOfInterest.Contains(txIn.PrevOut.Hash))
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

                        transactionsOfInterest.AddTentative(txId);
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

                    bool unconditional = transactionsOfInterest.Contains(txId); // Related to spending details.

                    IEnumerable<Script> destinations;
                    if (PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(txOut.ScriptPubKey))
                        destinations = new Script[] { txOut.ScriptPubKey };
                    else if (PayToPubkeyTemplate.Instance.CheckScriptPubKey(txOut.ScriptPubKey))
                        destinations = new Script[] { PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(txOut.ScriptPubKey).Hash.ScriptPubKey };
                    else
                        destinations = this.GetDestinations(txOut.ScriptPubKey);

                    // We need a script suitable for matching to HDAddress.ScriptPubKey.
                    foreach (Script pubKeyScript in destinations)
                    {
                        if (unconditional || addressesOfInterest.Contains(pubKeyScript)) // Paying to one of our addresses.
                        {
                            // We don't know which of these are actually received by our
                            // wallet addresses but we records them for batched resolution.
                            if (outputs == null)
                                outputs = TempTable.Create<TempOutput>();

                            outputs.Add(new TempOutput()
                            {
                                PubKey = pubKeyScript.ToHex(),              // For matching HDAddress.ScriptPubKey.
                                ScriptPubKey = txOut.ScriptPubKey.ToHex(),  // The ScriptPubKey from the txOut.
                                OutputBlockHeight = header?.Height ?? 0,
                                OutputBlockHash = header?.HashBlock.ToString(),
                                OutputTxIsCoinBase = (tx.IsCoinBase || tx.IsCoinStake) ? 1 : 0,
                                OutputTxTime = (int)tx.Time,
                                OutputTxId = txId.ToString(),
                                OutputIndex = i,
                                Value = txOut.Value.ToDecimal(MoneyUnit.BTC)
                            });

                            transactionsOfInterest.AddTentative(txId);
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