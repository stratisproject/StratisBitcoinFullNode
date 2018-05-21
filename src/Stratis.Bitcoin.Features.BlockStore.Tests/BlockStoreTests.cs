using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;
using Xunit;
using Moq;
using NBitcoin.DataEncoders;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockStoreTests
    {
        private BlockStore blockStore;
        private BlockRepositoryMock repository;
        private ConcurrentChain chain;
        private IChainState chainState;
        private NodeLifetime nodeLifetime;

        private ChainedHeader consensusTip;

        public BlockStoreTests()
        {
            this.repository = new BlockRepositoryMock();

            this.chainState = new Mock<IChainState>().SetupProperty(x => x.ConsensusTip, this.consensusTip).Object;

            this.chain = this.CreateChain(10);
            this.consensusTip = this.chain.Tip;
            this.nodeLifetime = new NodeLifetime();

            this.blockStore = new BlockStore(this.repository, this.chain, this.chainState, new StoreSettings(), this.nodeLifetime, new LoggerFactory());
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

        [Fact]
        public async Task BatchIsSavedAfter5MbLimitReachedAsync()
        {
            Block block = Block.Load(Encoders.Hex.DecodeData("000000202f6f6a130549473222411b5c6f54150d63b32aadf10e57f7d563cfc7010000001e28204471ef9ef11acd73543894a96a3044932b85e99889e731322a8ec28a9f9ae9fc56ffff011d0011b40202010000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff2c028027266a24aa21a9ed09154465f26a2a4144739eba3e83b3e9ae6a1f69566eae7dc3747d48f1183779010effffffff0250b5062a0100000023210263ed47e995cbbf1bc560101e3b76c6bdb1b094a185450cea533781ce598ff2b6ac0000000000000000266a24aa21a9ed09154465f26a2a4144739eba3e83b3e9ae6a1f69566eae7dc3747d48f1183779012000000000000000000000000000000000000000000000000000000000000000000000000001000000000101cecd90cd38ac6858c47f2fe9f28145d6e18f9c5abc7ef1a41e2f19e6fe0362580100000000ffffffff0130b48d06000000001976a91405481b7f1d90c5a167a15b00e8af76eb6984ea5988ac0247304402206104c335e4adbb920184957f9f710b09de17d015329fde6807b9d321fd2142db02200b24ad996b4aa4ff103000348b5ad690abfd9fddae546af9e568394ed4a83113012103a65786c1a48d4167aca08cf6eb8eed081e13f45c02dc6000fd8f3bb16242579a00000000"), Network.Main);
            int blockSize = block.GetSerializedSize();

            int count = (5 * 1000 * 1000) / blockSize + 2;

            ConcurrentChain longChain = this.CreateChain(count);
            this.consensusTip = longChain.Tip;
            this.repository.BlockHash = longChain.Genesis.HashBlock;

            this.blockStore = new BlockStore(this.repository, longChain, this.chainState, new StoreSettings(), new NodeLifetime(), new LoggerFactory());

            await this.blockStore.InitializeAsync().ConfigureAwait(false);

            for (int i = 1; i < count; ++i)
            {
                ChainedHeader header = longChain.GetBlock(i);

                this.blockStore.AddToPending(new BlockPair(block, header));
            }
            
            await Task.Delay(5000).ConfigureAwait(false);
            Assert.Equal(this.blockStore.StoreTip, longChain.GetBlock(count - 1));
            Assert.Equal(1, this.repository.SavesCount);
        }

        [Fact]
        public async Task BatchIsSavedOnShutdownAsync()
        {
            this.repository.BlockHash = this.chain.Genesis.HashBlock;
            
            await this.blockStore.InitializeAsync().ConfigureAwait(false);

            ChainedHeader lastHeader = null;

            for (int i = 1; i < this.chain.Height - 1; ++i)
            {
                lastHeader = this.chain.GetBlock(i);
                this.blockStore.AddToPending(new BlockPair(new Block(), lastHeader));
            }

            await Task.Delay(1000).ConfigureAwait(false);

            Assert.Equal(this.blockStore.StoreTip, this.chain.Genesis);
            Assert.Equal(0, this.repository.SavesCount);
            
            this.nodeLifetime.StopApplication();
            this.blockStore.Dispose();

            Assert.Equal(this.blockStore.StoreTip, lastHeader);
            Assert.Equal(1, this.repository.SavesCount);
        }

        [Fact]
        public async Task BatchIsSavedWhenAtTheTipAsync()
        {
            this.repository.BlockHash = this.chain.Genesis.HashBlock;
            this.consensusTip = this.chain.Tip;

            await this.blockStore.InitializeAsync().ConfigureAwait(false);

            ChainedHeader lastHeader = null;

            for (int i = 1; i <= this.chain.Height; ++i)
            {
                lastHeader = this.chain.GetBlock(i);
                this.blockStore.AddToPending(new BlockPair(new Block(), lastHeader));
            }

            await Task.Delay(1000).ConfigureAwait(false);
            
            Assert.Equal(this.blockStore.StoreTip, this.chain.Tip);
            Assert.Equal(1, this.repository.SavesCount);
        }
    }

    public class BlockRepositoryMock : IBlockRepository
    {
        /// <summary><see cref="GetAsync"/> will return blocks from this list.</summary>
        public Dictionary<uint256, Block> BlocksThatExist;
        
        public uint256 BlockHash { get; set; }

        public int SavesCount = 0;

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task PutAsync(uint256 nextBlockHash, List<Block> blocks)
        {
            this.BlockHash = nextBlockHash;
            this.SavesCount++;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(uint256 newBlockHash, List<uint256> hashes)
        {
            return Task.CompletedTask;
        }

        public Task SetBlockHashAsync(uint256 nextBlockHash)
        {
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
