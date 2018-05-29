using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockRepositoryInMemory : IBlockRepository
    {
        private ConcurrentDictionary<uint256, Block> store;
        public uint256 BlockHash { get; private set; }
        public bool TxIndex { get; private set; }
        public BlockStoreRepositoryPerformanceCounter PerformanceCounter { get; private set; }
        
        public BlockRepositoryInMemory()
        {
            this.InitializeAsync();
        }

        public Task InitializeAsync()
        {
            this.store = new ConcurrentDictionary<uint256, Block>();
            this.PerformanceCounter = new BlockStoreRepositoryPerformanceCounter(DateTimeProvider.Default);

            return Task.FromResult<object>(null);
        }

        public Task DeleteAsync(uint256 newlockHash, List<uint256> hashes)
        {
            Block block = null;

            foreach (var hash in hashes)
            {
                this.store.TryRemove(hash, out block);
            }

            this.SetBlockHashAsync(newlockHash);

            return Task.FromResult<object>(null);
        }

        public Task<bool> ExistAsync(uint256 hash)
        {
            return Task.FromResult(this.store.ContainsKey(hash));
        }

        public Task<Block> GetAsync(uint256 hash)
        {
            return Task.FromResult(this.store[hash]);
        }
        
        /// <inheritdoc />
        public Task<List<Block>> GetBlocksAsync(List<uint256> hashes)
        {
            return Task.FromResult(hashes.Select(hash => this.store.TryGetValue(hash, out Block block) ? block : null).ToList());
        }

        public Task PutAsync(uint256 nextBlockHash, List<Block> blocks)
        {
            foreach (var block in blocks)
            {
                this.store.TryAdd(block.Header.GetHash(), block);
            }

            this.SetBlockHashAsync(nextBlockHash);

            return Task.FromResult<object>(null);
        }

        public Task SetBlockHashAsync(uint256 nextBlockHash)
        {
            this.BlockHash = nextBlockHash;

            return Task.FromResult<object>(null);
        }

        public Task<Transaction> GetTrxAsync(uint256 trxid)
        {
            throw new NotImplementedException();
        }

        public Task<uint256> GetTrxBlockIdAsync(uint256 trxid)
        {
            throw new NotImplementedException();
        }

        public Task SetTxIndexAsync(bool txIndex)
        {
            this.TxIndex = txIndex;

            return Task.FromResult<object>(null);
        }

        #region IDisposable Support

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.store.Clear();
                    this.store = null;
                }

                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        #endregion IDisposable Support
    }
}
