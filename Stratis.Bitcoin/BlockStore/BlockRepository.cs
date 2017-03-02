using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.BlockStore
{
	public interface IBlockRepository
	{
		Task PutAsync(List<Block> blocks, bool txIndex);

		Task<Block> GetAsync(uint256 hash);

		Task<Transaction> GetTrxAsync(uint256 trxid);

		Task DeleteAsync(uint256 hash);
	}

	public class BlockRepository : IDisposable, IBlockRepository
	{
		readonly DBreezeSingleThreadSession session;
		readonly Network network;

		public BlockRepository(Network network, string folder)
		{
			if (folder == null)
				throw new ArgumentNullException("folder");
			if (network == null)
				throw new ArgumentNullException("network");

			this.session = new DBreezeSingleThreadSession("DBreeze BlockRepository", folder);
			this.network = network;
			Initialize(network.GetGenesis()).GetAwaiter().GetResult(); // hmm...
		}

		private Task Initialize(Block genesis)
		{
			var sync = this.session.Do(() =>
			{
				this.session.Transaction.SynchronizeTables("Block", "Transaction", "Common");
				this.session.Transaction.ValuesLazyLoadingIsOn = false;
			});

			var hash = this.session.Do(() =>
			{
				if (this.LoadBlockHash() == null)
				{
					this.FlushBlockHash(genesis.GetHash());
					this.session.Transaction.Commit();
				}
			});

			return Task.WhenAll(new[] {sync, hash});
		}

		public Task<Transaction> GetTrxAsync(uint256 trxid)
		{
			return this.session.Do(() =>
			{
				var blockid = this.session.Transaction.Select<byte[], uint256>("Transaction", trxid.ToBytes());
				if (blockid?.Value == null)
					return null;
				var block = this.session.Transaction.Select<byte[], Block>("Block", blockid.Value.ToBytes());
				return block?.Value?.Transactions.FirstOrDefault(t => t.GetHash() == trxid);
			});
		}

		static readonly byte[] BlockHashKey = new byte[0];
		public uint256 BlockHash { get; private set; }

		public Task PutAsync(List<Block> blocks, bool txIndex)
		{
			return this.session.Do(() =>
			{
				foreach (var block in blocks)
				{
					// if the block is already in store don't write it again
					const bool dontUpdateIfExists = true;
					var updated = false;
					byte[] bytes;
					var blockId = block.GetHash();
					this.session.Transaction.Insert<byte[], Block>("Block", blockId.ToBytes(), block, out bytes, out updated, dontUpdateIfExists);

					if (txIndex)
					{
						// index transactions
						foreach (var transaction in block.Transactions)
						{
							var trxId = transaction.GetHash();
							this.session.Transaction.Insert<byte[], uint256>("Transaction", trxId.ToBytes(), blockId, out bytes, out updated, dontUpdateIfExists);
						}
					}
				}

				this.session.Transaction.Commit();
			});
		}

		private uint256 LoadBlockHash()
		{
			this.BlockHash = this.BlockHash ?? this.session.Transaction.Select<byte[], uint256>("Common", BlockHashKey)?.Value;
			return this.BlockHash;
		}

		public Task SethBlockHash(uint256 nextBlockHash)
		{
			return this.session.Do(() =>
			{
				this.FlushBlockHash(nextBlockHash);
			});
		}

		private void FlushBlockHash(uint256 nextBlockHash)
		{
			this.BlockHash = nextBlockHash;
			this.session.Transaction.Insert<byte[], uint256>("Common", BlockHashKey, nextBlockHash);
		}

		public Task<Block> GetAsync(uint256 hash)
		{
			return this.session.Do(() =>
			{
				const bool readVisibilityScope = true;
				var key = hash.ToBytes();
				var item = this.session.Transaction.Select<byte[], Block>("Block", key, readVisibilityScope);
				return item?.Value;
			});
		}

		public Task DeleteAsync(uint256 hash)
		{
			return this.session.Do(() =>
			{
				// if the block is already in store don't write it again
				var key = hash.ToBytes();
				this.session.Transaction.RemoveKey<byte[]>("Block", key);
				this.session.Transaction.Commit();
			});
		}

		public void Dispose()
		{
			this.session.Dispose();
		}
	}
}
