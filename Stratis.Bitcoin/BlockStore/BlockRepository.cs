using System;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.BlockStore
{
	public interface IBlockRepository
	{
		Task PutAsync(Block block);

		Task<Block> GetAsync(uint256 hash);

		Task DeleteAsync(uint256 hash);
	}

	public class BlockRepository : IDisposable , IBlockRepository
	{
		readonly DBreezeSingleThreadSession session;

		public BlockRepository(string folder)
		{
			this.session = new DBreezeSingleThreadSession("DBreeze BlockRepository", folder);
		}

		public Task PutAsync(Block block)
		{
			return this.session.Do(() =>
			{
				// if the block is already in store don't write it again
				const bool dontUpdateIfExists = true;
				var updated = false;
				byte[] bytes;
				var key = block.GetHash().ToBytes();
				this.session.Transaction.Insert<byte[], Block>("Block", key, block, out bytes, out updated, dontUpdateIfExists);
				this.session.Transaction.Commit();
			});
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
