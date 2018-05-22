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
        private readonly IBlockRepository repository;
        private readonly ConcurrentChain chain;
        private readonly IChainState chainState;
        private readonly NodeLifetime nodeLifetime;

        private uint256 repositoryBlockHash;
        private int repositorySavesCount = 0;

        private ChainedHeader chainStateBlockStoreTip;

        private ChainedHeader consensusTip;

        public BlockStoreTests()
        {
            var reposMoq = new Mock<IBlockRepository>();
            reposMoq.Setup(x => x.PutAsync(It.IsAny<uint256>(), It.IsAny<List<Block>>())).Returns((uint256 nextBlockHash, List<Block> blocks) =>
            {
                this.repositoryBlockHash = nextBlockHash;
                this.repositorySavesCount++;
                return Task.CompletedTask;
            });

            reposMoq.Setup(x => x.BlockHash).Returns(() =>
            {
                return this.repositoryBlockHash;
            });

            this.repository = reposMoq.Object;

            var chainStateMoq = new Mock<IChainState>().SetupProperty(x => x.ConsensusTip, this.consensusTip);
            chainStateMoq.SetupProperty(x => x.BlockStoreTip, this.chainStateBlockStoreTip);

            this.chainState = chainStateMoq.Object;

            this.chain = this.CreateChain(10);
            this.consensusTip = this.chain.Tip;
            this.nodeLifetime = new NodeLifetime();

            this.blockStore = new BlockStore(this.repository, this.chain, this.chainState, new StoreSettings(), this.nodeLifetime, new LoggerFactory());
        }

        private ConcurrentChain CreateChain(int blocksCount)
        {
            var chain = new ConcurrentChain(Network.StratisMain);

            for (int i = 0; i < blocksCount; i++)
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

        private async Task WaitUntilQueueIsEmptyAsync()
        {
            while (this.blockStore.BlocksQueueCount != 0)
                await Task.Delay(100).ConfigureAwait(false);
            
            // For very slow environments.
            await Task.Delay(100).ConfigureAwait(false);
        }

        /// <summary>Checks that block store tip initializes from the hash of the last saved block.</summary>
        [Fact]
        public async Task BlockStoreInitializesTipAtHashOfLastSavedBlockAsync()
        {
            ChainedHeader initializationHeader = this.chain.Tip.Previous.Previous.Previous;
            this.repositoryBlockHash = initializationHeader.HashBlock;

            await this.blockStore.InitializeAsync().ConfigureAwait(false);
            Assert.Equal(initializationHeader, this.chainState.BlockStoreTip);
        }
        
        [Fact]
        public async Task BatchIsSavedAfter5MbLimitReachedAsync()
        {
            string blockHex = "000000202f6f6a130549473222411b5c6f54150d63b32aadf10e57f7d563cfc7010000001e28204471ef9ef11acd73543894a96a3044932b85e99889e731322a8ec28a9f9ae9fc56ffff011d0011b40202010000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff2c028027266a24aa21a9ed09154465f26a2a4144739eba3e83b3e9ae6a1f69566eae7dc3747d48f1183779010effffffff0250b5062a0100000023210263ed47e995cbbf1bc560101e3b76c6bdb1b094a185450cea533781ce598ff2b6ac0000000000000000266a24aa21a9ed09154465f26a2a4144739eba3e83b3e9ae6a1f69566eae7dc3747d48f1183779012000000000000000000000000000000000000000000000000000000000000000000000000001000000000101cecd90cd38ac6858c47f2fe9f28145d6e18f9c5abc7ef1a41e2f19e6fe0362580100000000ffffffff0130b48d06000000001976a91405481b7f1d90c5a167a15b00e8af76eb6984ea5988ac0247304402206104c335e4adbb920184957f9f710b09de17d015329fde6807b9d321fd2142db02200b24ad996b4aa4ff103000348b5ad690abfd9fddae546af9e568394ed4a83113012103a65786c1a48d4167aca08cf6eb8eed081e13f45c02dc6000fd8f3bb16242579a00000000";
            Block block = Block.Load(Encoders.Hex.DecodeData(blockHex), Network.Main);
            int blockSize = block.GetSerializedSize();

            int count = (5 * 1000 * 1000) / blockSize + 2;

            ConcurrentChain longChain = this.CreateChain(count);
            this.consensusTip = longChain.Tip;
            this.repositoryBlockHash = longChain.Genesis.HashBlock;

            this.blockStore = new BlockStore(this.repository, longChain, this.chainState, new StoreSettings(), new NodeLifetime(), new LoggerFactory());

            await this.blockStore.InitializeAsync().ConfigureAwait(false);

            for (int i = 1; i < count; i++)
            {
                ChainedHeader header = longChain.GetBlock(i);

                this.blockStore.AddToPending(new BlockPair(block, header));
            }
            
            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);
            Assert.Equal(longChain.GetBlock(count - 1), this.chainState.BlockStoreTip);
            Assert.Equal(1, this.repositorySavesCount);
        }

        [Fact]
        public async Task BatchIsSavedOnShutdownAsync()
        {
            this.repositoryBlockHash = this.chain.Genesis.HashBlock;
            
            await this.blockStore.InitializeAsync().ConfigureAwait(false);

            ChainedHeader lastHeader = null;

            for (int i = 1; i < this.chain.Height - 1; i++)
            {
                lastHeader = this.chain.GetBlock(i);
                this.blockStore.AddToPending(new BlockPair(new Block(), lastHeader));
            }

            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);

            Assert.Equal(this.chainState.BlockStoreTip, this.chain.Genesis);
            Assert.Equal(0, this.repositorySavesCount);
            
            this.nodeLifetime.StopApplication();
            this.blockStore.Dispose();

            Assert.Equal(this.chainState.BlockStoreTip, lastHeader);
            Assert.Equal(1, this.repositorySavesCount);
        }

        [Fact]
        public async Task BatchIsSavedWhenAtConsensusTipAsync()
        {
            this.repositoryBlockHash = this.chain.Genesis.HashBlock;
            this.consensusTip = this.chain.Tip;

            await this.blockStore.InitializeAsync().ConfigureAwait(false);

            for (int i = 1; i <= this.chain.Height; i++)
            {
                ChainedHeader lastHeader = this.chain.GetBlock(i);
                this.blockStore.AddToPending(new BlockPair(new Block(), lastHeader));
            }

            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);
            
            Assert.Equal(this.chainState.BlockStoreTip, this.chain.Tip);
            Assert.Equal(1, this.repositorySavesCount);
        }
    }
}
