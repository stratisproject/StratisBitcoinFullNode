using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;
using Xunit;
using Moq;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockStoreTests
    {
        private BlockStore blockStore;
        private BlockRepositoryMock repository;
        private ConcurrentChain chain;

        private ChainedHeader consensusTip;

        public BlockStoreTests()
        {
            this.repository = new BlockRepositoryMock();

            Mock<IChainState> chainState = new Mock<IChainState>().SetupProperty(x => x.ConsensusTip, this.consensusTip);

            this.chain = this.CreateChain(10);
            this.consensusTip = this.chain.Tip;

            this.blockStore = new BlockStore(this.repository, chain, chainState.Object, new StoreSettings(), new NodeLifetime(), new LoggerFactory());
        }

        private ConcurrentChain CreateChain(int blocksCount)
        {
            var chain = new ConcurrentChain(Network.StratisMain);

            for (int i = 0; i < blocksCount; ++i)
            {
                var header = new BlockHeader()
                {
                    Nonce = RandomUtils.GetUInt32(),
                    HashPrevBlock = chain.Tip.HashBlock,
                    Bits = Target.Difficulty1
                };
                
                var chainedHeader = new ChainedHeader(header, header.GetHash(), chain.Tip);

                chain.SetTip(chainedHeader);
            }

            return chain;
        }

        /// <summary>Checks that block store tip initializes from the hash of the last saved block.</summary>
        [Fact]
        public async Task BlockStoreInitializesTipAtHashBlockAsync()
        {
            ChainedHeader initializationHeader = this.chain.Tip.Previous.Previous.Previous;
            this.repository.BlockHash = initializationHeader.HashBlock;

            await this.blockStore.InitializeAsync().ConfigureAwait(false);
            Assert.Equal(initializationHeader, this.blockStore.StoreTip);
        }
        
        [Fact]
        public async Task BlockStoreSavesBatchRightAwayWhenAtConsensusTipAsync()
        {
            this.repository.BlockHash = this.chain.Tip.Previous.HashBlock;

            await this.blockStore.InitializeAsync().ConfigureAwait(false);

            this.blockStore.AddToPending(new BlockPair(new Block(), this.chain.Tip));

            await Task.Delay(1000).ConfigureAwait(false);

            Assert.Equal(this.blockStore.StoreTip, this.chain.Tip);
            Assert.Equal(this.chain.Tip.HashBlock, this.repository.BlockHash);
        }

        //TODO
        /*
         * Make sure that at the tip- saving all
         * Test that we can reorg
         * Test edgecases when we need reorg batch & batch and repository
         * Test batch saving on time or on size
         */
    }


    public class BlockRepositoryMock : IBlockRepository
    {
        /// <summary><see cref="GetAsync"/> will return blocks from this list.</summary>
        public Dictionary<uint256, Block> BlocksThatExist;
        
        public uint256 BlockHash { get; set; }

        public Task InitializeAsync()
        {
            //TODO
            return Task.CompletedTask;
        }

        public Task PutAsync(uint256 nextBlockHash, List<Block> blocks)
        {
            this.BlockHash = nextBlockHash;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(uint256 newBlockHash, List<uint256> hashes)
        {
            //TODO
            return Task.CompletedTask;
        }

        public Task SetBlockHashAsync(uint256 nextBlockHash)
        {
            //TODO
            return Task.CompletedTask;
        }

        public Task<Block> GetAsync(uint256 hash)
        {
            if (this.BlocksThatExist == null || !this.BlocksThatExist.ContainsKey(hash))
                return Task.FromResult((Block)null);

            return Task.FromResult(this.BlocksThatExist[hash]);
        }

        public Task<List<Block>> GetBlocksAsync(List<uint256> hashes)
        {
            throw new NotImplementedException();
        }

        public Task<Transaction> GetTrxAsync(uint256 trxid)
        {
            throw new NotImplementedException();
        }
 
        public Task<bool> ExistAsync(uint256 hash)
        {
            throw new NotImplementedException();
        }

        public Task<uint256> GetTrxBlockIdAsync(uint256 trxid)
        {
            throw new NotImplementedException();
        }
         
        public Task SetTxIndexAsync(bool txIndex)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }

        public BlockStoreRepositoryPerformanceCounter PerformanceCounter { get; }

        public bool TxIndex { get; }
    }
}
