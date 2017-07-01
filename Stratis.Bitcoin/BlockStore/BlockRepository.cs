using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using DBreeze.Utils;

namespace Stratis.Bitcoin.BlockStore
{
	public interface IBlockRepository : IDisposable
	{
		Task Initialize();

		Task PutAsync(uint256 nextBlockHash, List<Block> blocks);

		Task<Block> GetAsync(uint256 hash);

		Task<Transaction> GetTrxAsync(uint256 trxid);

        Task<List<uint256>> GetAddrTrxHashesAsync(uint160 addr);

		Task DeleteAsync(uint256 newlockHash, List<uint256> hashes);

		Task<bool> ExistAsync(uint256 hash);

		Task<uint256> GetTrxBlockIdAsync(uint256 trxid);

		Task SetBlockHash(uint256 nextBlockHash);

		Task SetTxIndex(bool txIndex);
	}

	public class BlockRepository : IBlockRepository
	{
		private readonly DBreezeSingleThreadSession session;
		private readonly Network network;
		private static readonly byte[] BlockHashKey = new byte[0];
		private static readonly byte[] TxIndexKey = new byte[1];
		public BlockStoreRepositoryPerformanceCounter PerformanceCounter { get; }

		public BlockRepository(Network network, DataFolder dataFolder)
			: this(network, dataFolder.BlockPath)
		{
		}

		public BlockRepository(Network network, string folder)
		{
			Guard.NotNull(network, nameof(network));
			Guard.NotEmpty(folder, nameof(folder));

			this.session = new DBreezeSingleThreadSession("DBreeze BlockRepository", folder);
			this.network = network;
			this.PerformanceCounter = new BlockStoreRepositoryPerformanceCounter();
		}

		public Task Initialize()
		{
			var genesis = this.network.GetGenesis();

			var sync = this.session.Do(() =>
			{
				this.session.Transaction.SynchronizeTables("Block", "Transaction", "Address", "Common");
				this.session.Transaction.ValuesLazyLoadingIsOn = true;
			});

			var hash = this.session.Do(() =>
			{
				if (this.LoadBlockHash() == null)
				{
					this.SaveBlockHash(genesis.GetHash());
					this.session.Transaction.Commit();
				}
				if (this.LoadTxIndex() == null)
				{
					this.SaveTxIndex(false);
					this.session.Transaction.Commit();
				}
			});

			return Task.WhenAll(new[] { sync, hash });
		}

		public bool LazyLoadingOn
		{
			get { return this.session.Transaction.ValuesLazyLoadingIsOn; }
			set { this.session.Transaction.ValuesLazyLoadingIsOn = value; }
		}

        public Task<Transaction> GetTrxAsync(uint256 trxid)
		{
			Guard.NotNull(trxid, nameof(trxid));

			if (!this.TxIndex)
				return Task.FromResult(default(Transaction));

			return this.session.Do(() =>
			{
				var blockid = this.session.Transaction.Select<byte[], uint256>("Transaction", trxid.ToBytes());
                if (!blockid.Exists)
                {
                    this.PerformanceCounter.AddRepositoryMissCount(1);
                    return null;
                }

                this.PerformanceCounter.AddRepositoryHitCount(1);
                var block = this.session.Transaction.Select<byte[], Block>("Block", blockid.Value.ToBytes());
				var trx = block?.Value?.Transactions.FirstOrDefault(t => t.GetHash() == trxid);

                if (trx == null)
                {
                    this.PerformanceCounter.AddRepositoryMissCount(1);
                }
                else
                {
                    this.PerformanceCounter.AddRepositoryHitCount(1);
                }

                return trx;
            });
		}

        public Task<List<uint256>> GetAddrTrxHashesAsync(uint160 addr)
        {
            Guard.NotNull(addr, nameof(addr));

            if (!this.TxIndex)
                return Task.FromResult(default(List<uint256>));

            return this.session.Do(() =>
            {
                return this.GetAddressTransactionHashes(addr.ToBytes()).ToList();
            });
        }

        public Task<uint256> GetTrxBlockIdAsync(uint256 trxid)
		{
			Guard.NotNull(trxid, nameof(trxid));

			if (!this.TxIndex)
				return Task.FromResult(default(uint256));

			return this.session.Do(() =>
			{
				var blockid = this.session.Transaction.Select<byte[], uint256>("Transaction", trxid.ToBytes());

                if (!blockid.Exists)
                {
                    this.PerformanceCounter.AddRepositoryMissCount(1);
                    return null;
                }
                else
                {
                    this.PerformanceCounter.AddRepositoryHitCount(1);
                    return blockid.Value;
                }
			});
		}		

		public uint256 BlockHash { get; private set; }
		public bool TxIndex { get; private set; }

		public Task PutAsync(uint256 nextBlockHash, List<Block> blocks)
		{
			Guard.NotNull(nextBlockHash, nameof(nextBlockHash));
			Guard.NotNull(blocks, nameof(blocks));

            // dbreeze is faster if sort ascending by key in memory before insert
            // however we need to find how byte arrays are sorted in dbreeze this link can help 
            // https://docs.google.com/document/pub?id=1IFkXoX3Tc2zHNAQN9EmGSXZGbabMrWmpmVxFsLxLsw

            // Use this comparer. We are assuming that DBreeze would use the same comparer for
            // ordering rows in the file.
            var byteListComparer = new ByteListComparer();

            return this.session.Do(() =>
			{
                Dictionary<uint256, Block> blockDict = new Dictionary<uint256, Block>();
                Dictionary<uint256, uint256> transDict = new Dictionary<uint256, uint256>();
                Dictionary<uint160, HashSet<uint256>> addrDict = new Dictionary<uint160, HashSet<uint256>>();

                // Gather blocks
                foreach (var block in blocks)
                {
                    var blockId = block.GetHash();
                    blockDict[blockId] = block;
                    // Gaher transactions
                    if (this.TxIndex)
                    {
                        foreach (var transaction in block.Transactions)
                        {
                            var trxId = transaction.GetHash();
                            transDict[trxId] = blockId;
                            // Gather addresses
                            foreach (var addr in this.IndexedTransactionAddresses(transaction))
                            {
                                HashSet<uint256> list = addrDict.TryGet(addr);
                                if (list == null)
                                {
                                    list = new HashSet<uint256>();
                                    addrDict[addr] = list;
                                }
                                list.Add(trxId);
                            }
                        }
                    }
                }

                // Sort blocks. Be consistent in always converting our keys to byte arrays using the ToBytes method.
                var blockList = blockDict.ToList();      
                blockList.Sort((pair1, pair2) => byteListComparer.Compare(pair1.Key.ToBytes(), pair2.Key.ToBytes()));

                // Index blocks
                foreach (KeyValuePair<uint256, Block> kv in blockList)
                {
                    var blockId = kv.Key;
                    var block = kv.Value;

                    // if the block is already in store don't write it again
                    var item = this.session.Transaction.Select<byte[], Block>("Block", blockId.ToBytes());
                    if (!item.Exists)
                    {
                        this.PerformanceCounter.AddRepositoryMissCount(1);
                        this.PerformanceCounter.AddRepositoryInsertCount(1);
                        this.session.Transaction.Insert<byte[], Block>("Block", blockId.ToBytes(), block);
                    }
                    else
                    {
                        this.PerformanceCounter.AddRepositoryHitCount(1);
                    }
                }

                // Sort blocks. Be consistent in always converting our keys to byte arrays using the ToBytes method.
                var transList = transDict.ToList();
                transList.Sort((pair1, pair2) => byteListComparer.Compare(pair1.Key.ToBytes(), pair2.Key.ToBytes()));

                // Index transactions
                foreach (KeyValuePair<uint256, uint256> kv in transList)
                {
                    var trxId = kv.Key;
                    var blockId = kv.Value;

                    this.PerformanceCounter.AddRepositoryInsertCount(1);
                    this.session.Transaction.Insert<byte[], uint256>("Transaction", trxId.ToBytes(), blockId);
                }

                // Sort blocks. Be consistent in always converting our keys to byte arrays using the ToBytes method.
                var addrList = addrDict.ToList();
                addrList.Sort((pair1, pair2) => byteListComparer.Compare(pair1.Key.ToBytes(), pair2.Key.ToBytes()));

                // Index addresses
                foreach (KeyValuePair<uint160, HashSet<uint256>> kv in addrList)
                {
                    var address = kv.Key;
                    var transactions = kv.Value;

                    this.PerformanceCounter.AddRepositoryInsertCount(1);
                    this.InsertAddressTransactions(address, transactions);
                }

                // Commit additions
                this.SaveBlockHash(nextBlockHash);
				this.session.Transaction.Commit();
			});
		}

        private IEnumerable<uint160> IndexedTransactionAddresses(Transaction transaction)
        {
            // TODO: Add additional addresses
            foreach (var output in transaction.Outputs)
            {
                var script = output.ScriptPubKey.ToBytes(true);
                if (script.Length == 25 && script[0] == (byte)OpcodeType.OP_DUP && script[1] == (byte)OpcodeType.OP_HASH160 && 
                    script[23] == (byte)OpcodeType.OP_EQUALVERIFY && script[24] == (byte)OpcodeType.OP_CHECKSIG)
                {
                    var bytes = new byte[20];
                    Array.Copy(script, 3, bytes, 0, 20);             
                    yield return new uint160(bytes);
                }
            }
        }

        private bool? LoadTxIndex()
		{
			var item = this.session.Transaction.Select<byte[], bool>("Common", TxIndexKey);

            if (!item.Exists)
            {
                this.PerformanceCounter.AddRepositoryMissCount(1);
                return null;
            }
            else
            {
                this.PerformanceCounter.AddRepositoryHitCount(1);
                this.TxIndex = item.Value;
                return item.Value;
            }
		}
		private void SaveTxIndex(bool txIndex)
		{
			this.TxIndex = txIndex;
            this.PerformanceCounter.AddRepositoryInsertCount(1);
			this.session.Transaction.Insert<byte[], bool>("Common", TxIndexKey, txIndex);
		}

		public Task SetTxIndex(bool txIndex)
		{
			return this.session.Do(() =>
			{
				this.SaveTxIndex(txIndex);
				this.session.Transaction.Commit();
			});
		}

		private uint256 LoadBlockHash()
		{
			this.BlockHash = this.BlockHash ?? this.session.Transaction.Select<byte[], uint256>("Common", BlockHashKey)?.Value;
			return this.BlockHash;
		}

		public Task SetBlockHash(uint256 nextBlockHash)
		{
			Guard.NotNull(nextBlockHash, nameof(nextBlockHash));

			return this.session.Do(() =>
			{
				this.SaveBlockHash(nextBlockHash);
				this.session.Transaction.Commit();
			});
		}

		private void SaveBlockHash(uint256 nextBlockHash)
		{
			this.BlockHash = nextBlockHash;
            this.PerformanceCounter.AddRepositoryInsertCount(1);
			this.session.Transaction.Insert<byte[], uint256>("Common", BlockHashKey, nextBlockHash);
		}

		public Task<Block> GetAsync(uint256 hash)
		{
			Guard.NotNull(hash, nameof(hash));

			return this.session.Do(() =>
			{
				var key = hash.ToBytes();                
                var item = this.session.Transaction.Select<byte[], Block>("Block", key);
                if (!item.Exists)
                {
                    this.PerformanceCounter.AddRepositoryMissCount(1);
                }
                else
                {
                    this.PerformanceCounter.AddRepositoryHitCount(1);
                }

                return item?.Value;                
			});
		}

		public Task<bool> ExistAsync(uint256 hash)
		{
			Guard.NotNull(hash, nameof(hash));

			return this.session.Do(() =>
			{
				var key = hash.ToBytes();
				var item = this.session.Transaction.Select<byte[], Block>("Block", key);
                if (!item.Exists)
                {
                    this.PerformanceCounter.AddRepositoryMissCount(1);                    
                }
                else
                {
                    this.PerformanceCounter.AddRepositoryHitCount(1);                
                }

                return item.Exists; // lazy loading is on so we don't fetch the whole value, just the row.
            });
		}

		public Task DeleteAsync(uint256 newlockHash, List<uint256> hashes)
		{
			Guard.NotNull(newlockHash, nameof(newlockHash));
			Guard.NotNull(hashes, nameof(hashes));

            return this.session.Do(() =>
            {
                foreach (var hash in hashes)
                {
                    // if the block is already in store don't write it again
                    var key = hash.ToBytes();

                    if (this.TxIndex)
                    {
                        var block = this.session.Transaction.Select<byte[], Block>("Block", key);
                        if (block.Exists)
                        {
                            this.PerformanceCounter.AddRepositoryHitCount(1);

                            foreach (var transaction in block.Value.Transactions)
                            {
                                this.PerformanceCounter.AddRepositoryDeleteCount(1);
                                this.session.Transaction.RemoveKey<byte[]>("Transaction", transaction.GetHash().ToBytes());

                                // Remove transaction reference from indexed addresses
                                foreach (var addr in this.IndexedTransactionAddresses(transaction))
                                {
                                    if (this.RemoveTransaction(addr, transaction))
                                        this.PerformanceCounter.AddRepositoryDeleteCount(1);
                                }
                            }
                        }
                        else
                        {
                            this.PerformanceCounter.AddRepositoryMissCount(1);
                        }
			        }

                    this.PerformanceCounter.AddRepositoryDeleteCount(1);
                    this.session.Transaction.RemoveKey<byte[]>("Block", key);
			    }

			    this.SaveBlockHash(newlockHash);
			    this.session.Transaction.Commit();
			});
		}

        private IEnumerable<uint256> GetAddressTransactionHashes(byte[] addr)
        {
            var addrRow = this.session.Transaction.Select<byte[], byte[]>("Address", addr);
            if (addrRow.Exists)
            {
                // Get the number of transactions
                var cntBytes = addrRow.GetValuePart(0, sizeof(uint));
                if (!BitConverter.IsLittleEndian) Array.Reverse(cntBytes);
                uint cnt = BitConverter.ToUInt32(cntBytes, 0);

                // Any transactions recorded?
                if (cnt > 0)
                {
                    byte[] listBytes = addrRow.GetValuePart(sizeof(uint), (uint)(cnt * 32));
                    for (int i = 0; i < cnt; i++)
                    {
                        var trxid = new byte[32];
                        Array.Copy(listBytes, i * 32, trxid, 0, 32);
                        yield return new uint256(trxid);
                    }
                }
            }
        }

        private void InsertAddressTransactions(uint160 addr, HashSet<uint256> list)
        {
            uint cnt = 0;
            var addrRow = this.session.Transaction.Select<byte[], byte[]>("Address", addr.ToBytes());

            if (addrRow.Exists)
            {
                // Get the number of transaction hashes
                var bytes = addrRow.GetValuePart(0, sizeof(uint));
                if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
                cnt = BitConverter.ToUInt32(bytes, 0);
            }

            // Force the value to a power of 2 that will fit all the additional transaction hashes
            if ((cnt + list.Count) >= 2 && (addrRow.Value == null || (sizeof(uint) + (cnt + list.Count) * 32) > addrRow.Value.Length))
            {
                uint growCnt = 2;
                for (; growCnt < (cnt + list.Count); growCnt *= 2) { }
                uint growSize = sizeof(uint) + growCnt * 32;
                this.session.Transaction.InsertPart<byte[], byte[]>("Address", addr.ToBytes(), new byte[] { 0 /* Dummy */}, growSize - 1);
            }

            // Add the transaction hashes
            foreach (var trxId in list)
                this.session.Transaction.InsertPart<byte[], byte[]>("Address", addr.ToBytes(), trxId.ToBytes(), sizeof(uint) + (cnt++) * 32);

            // Update the number of transaction hashes
            var cntBytes = BitConverter.GetBytes(cnt);
            if (!BitConverter.IsLittleEndian) Array.Reverse(cntBytes);
            
            this.session.Transaction.InsertPart<byte[], byte[]>("Address", addr.ToBytes(), cntBytes, 0);
        }

        private bool RemoveTransaction(uint160 addr, Transaction transaction)
        {
            var trxid = transaction.GetHash().ToBytes();

            var addrRow = this.session.Transaction.Select<byte[], byte[]>("Address", addr.ToBytes());
            if (!addrRow.Exists)
                return false;

            // Storing one-to-many value as count followed by count transaction hashes
            
            // Get the number of transactions
            var cntBytes = addrRow.GetValuePart(0, sizeof(uint));
            if (!BitConverter.IsLittleEndian) Array.Reverse(cntBytes);
            uint cnt = BitConverter.ToUInt32(cntBytes, 0);
            bool found = false;

            // Any transactions recorded?
            if (cnt > 0)
            {
                // Get all the transactions hashes for easy iteration
                byte[] listBytes = addrRow.GetValuePart(sizeof(uint), (uint)(cnt * trxid.Length));

                // Traverse in reverse order
                int i = listBytes.Length - trxid.Length;
                for (; i >= 0; i -= trxid.Length)
                {
                    int j = 0;
                    // trxid found?
                    for (; j < trxid.Length; j++)
                        if (listBytes[i + j] != trxid[j])
                            break;
                    found = (j == trxid.Length);
                    if (found) break;
                }

                // Transaction was not found
                if (!found) return false;

                // Found the trxid. Now remove it by overwriting the rest of the list with a shifted version
                var replaceBytes = new byte[listBytes.Length - i];
                Array.Copy(listBytes, i + trxid.Length, replaceBytes, 0, replaceBytes.Length - trxid.Length);

                // Overwrite the value in the table row
                this.session.Transaction.InsertPart<byte[], byte[]>("Address", addr.ToBytes(), replaceBytes, (uint)(i + sizeof(uint)));

                // Update the number of items
                if (--cnt > 0)
                {
                    var cntBytes2 = BitConverter.GetBytes(cnt);
                    if (!BitConverter.IsLittleEndian) Array.Reverse(cntBytes2);
                    this.session.Transaction.InsertPart<byte[], byte[]>("Address", addr.ToBytes(), cntBytes2, 0);
                    return true;
                }
            }

            // Key is no longer needed
            if (cnt == 0)
                this.session.Transaction.RemoveKey<byte[]>("Address", addr.ToBytes());

            return found;
        }

		public void Dispose()
		{
			this.session.Dispose();
		}
	}
}
