using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private readonly IChainState chainState;
        private readonly NodeLifetime nodeLifetime;
        private ConcurrentChain chain;

        private uint256 repositoryBlockHash;
        private int repositorySavesCount = 0;
        private int repositoryTotalBlocksSaved = 0;
        private int repositoryTotalBlocksDeleted = 0;

        private ChainedHeader chainStateBlockStoreTip;

        private ChainedHeader consensusTip;

        public BlockStoreTests()
        {
            var reposMoq = new Mock<IBlockRepository>();
            reposMoq.Setup(x => x.PutAsync(It.IsAny<uint256>(), It.IsAny<List<Block>>())).Returns((uint256 nextBlockHash, List<Block> blocks) =>
            {
                this.repositoryBlockHash = nextBlockHash;
                this.repositorySavesCount++;
                this.repositoryTotalBlocksSaved += blocks.Count;
                return Task.CompletedTask;
            });

            reposMoq.Setup(x => x.DeleteAsync(It.IsAny<uint256>(), It.IsAny<List<uint256>>())).Returns((uint256 nextBlockHash, List<uint256> blocks) =>
            {
                this.repositoryTotalBlocksDeleted += blocks.Count;
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
            int iterations = 0;

            var queue = this.blockStore.GetMemberValue("blocksQueue") as AsyncQueue<BlockPair>;
            
            while (true)
            {
                int itemsCount = ((Queue<BlockPair>)queue.GetMemberValue("items")).Count;

                if (itemsCount != 0)
                    await Task.Delay(100).ConfigureAwait(false);
                else
                    break;

                iterations++;

                if (iterations > 500)
                    throw new Exception("Unexpected queue processing delay!");
            }
            
            // For very slow environments.
            await Task.Delay(500).ConfigureAwait(false);
        }
        
        [Fact]
        public async Task BlockStoreInitializesTipAtHashOfLastSavedBlockAsync()
        {
            ChainedHeader initializationHeader = this.chain.Tip.Previous.Previous.Previous;
            this.repositoryBlockHash = initializationHeader.HashBlock;

            await this.blockStore.InitializeAsync().ConfigureAwait(false);
            Assert.Equal(initializationHeader, this.chainState.BlockStoreTip);
        }
        
        [Fact]
        public async Task BatchIsSavedAfterSizeThresholdReachedAsync()
        {
            string blockHex = "000000202f6f6a130549473222411b5c6f54150d63b32aadf10e57f7d563cfc7010000001e28204471ef9ef11acd73543894a96a3044932b85e99889e731322a8ec28a9f9ae9fc56ffff011d0011b40202010000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff2c028027266a24aa21a9ed09154465f26a2a4144739eba3e83b3e9ae6a1f69566eae7dc3747d48f1183779010effffffff0250b5062a0100000023210263ed47e995cbbf1bc560101e3b76c6bdb1b094a185450cea533781ce598ff2b6ac0000000000000000266a24aa21a9ed09154465f26a2a4144739eba3e83b3e9ae6a1f69566eae7dc3747d48f1183779012000000000000000000000000000000000000000000000000000000000000000000000000001000000000101cecd90cd38ac6858c47f2fe9f28145d6e18f9c5abc7ef1a41e2f19e6fe0362580100000000ffffffff0130b48d06000000001976a91405481b7f1d90c5a167a15b00e8af76eb6984ea5988ac0247304402206104c335e4adbb920184957f9f710b09de17d015329fde6807b9d321fd2142db02200b24ad996b4aa4ff103000348b5ad690abfd9fddae546af9e568394ed4a83113012103a65786c1a48d4167aca08cf6eb8eed081e13f45c02dc6000fd8f3bb16242579a00000000";
            Block block = Block.Load(Encoders.Hex.DecodeData(blockHex), Network.Main);
            int blockSize = block.GetSerializedSize();

            int count = BlockStore.BatchThresholdSizeBytes / blockSize + 2;

            ConcurrentChain longChain = this.CreateChain(count);
            this.consensusTip = longChain.Tip;
            this.repositoryBlockHash = longChain.Genesis.HashBlock;

            this.blockStore = new BlockStore(this.repository, longChain, this.chainState, new StoreSettings(), new NodeLifetime(), new LoggerFactory());

            await this.blockStore.InitializeAsync().ConfigureAwait(false);

            // Send all the blocks to the block store except for the last one because that will trigger batch saving because of reaching the tip.
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

        [Fact]
        public async Task ReorgedBlocksAreNotSavedAsync()
        {
            this.repositoryBlockHash = this.chain.Genesis.HashBlock;

            await this.blockStore.InitializeAsync().ConfigureAwait(false);

            int reorgedChainLenght = 3;
            int realChainLenght = 6;
            
            // First present a short chain.
            ConcurrentChain alternativeChain = this.CreateChain(reorgedChainLenght);
            for (int i = 1; i < alternativeChain.Height; i++)
                this.blockStore.AddToPending(new BlockPair(new Block(), alternativeChain.GetBlock(i)));

            // Present second chain which has more work and reorgs blocks from genesis. 
            for (int i = 1; i < realChainLenght; i++)
                this.blockStore.AddToPending(new BlockPair(new Block(), this.chain.GetBlock(i)));
            
            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);

            Assert.Equal(this.chainState.BlockStoreTip, this.chain.Genesis);
            Assert.Equal(0, this.repositorySavesCount);

            // Dispose block store to trigger save.
            this.nodeLifetime.StopApplication();
            this.blockStore.Dispose();
            
            // Make sure that blocks only from 2nd chain were saved.
            Assert.Equal(this.chain.GetBlock(realChainLenght - 1), this.chainState.BlockStoreTip);
            Assert.Equal(1, this.repositorySavesCount);
            Assert.Equal(realChainLenght - 1, this.repositoryTotalBlocksSaved);
        }

        /// <summary>
        /// Tests reorgs inside the batch and inside the database at the same time.
        /// </summary>
        [Fact]
        public async Task ReorgedBlocksAreDeletedFromRepositoryIfReorgDetectedAsync()
        {
            this.chain = this.CreateChain(1000);
            this.consensusTip = this.chain.Tip;
            this.repositoryBlockHash = this.chain.Genesis.HashBlock;

            this.blockStore = new BlockStore(this.repository, this.chain, this.chainState, new StoreSettings(), this.nodeLifetime, new LoggerFactory());

            await this.blockStore.InitializeAsync().ConfigureAwait(false);

            // Sending 500 blocks to the queue.
            for (int i = 1; i < 500; i++)
                this.blockStore.AddToPending(new BlockPair(new Block(), this.chain.GetBlock(i)));
            
            // Create alternative chain with fork point at 450.
            ChainedHeader prevBlock = this.chain.GetBlock(450);
            var alternativeBlocks = new List<ChainedHeader>();
            for (int i = 0; i < 100; i++)
            {
                var header = new BlockHeader()
                {
                    Nonce = RandomUtils.GetUInt32(),
                    HashPrevBlock = prevBlock.HashBlock,
                    Bits = Target.Difficulty1
                };

                var chainedHeader = new ChainedHeader(header, header.GetHash(), prevBlock);
                alternativeBlocks.Add(chainedHeader);
                prevBlock = chainedHeader;
            }

            ChainedHeader savedHeader = this.chain.Tip;

            this.chain.SetTip(alternativeBlocks.Last());
            this.consensusTip = this.chain.Tip;

            // Present alternative chain and trigger save.
            foreach (ChainedHeader header in alternativeBlocks)
                this.blockStore.AddToPending(new BlockPair(new Block(), header));

            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);

            // Make sure only longest chain is saved.
            Assert.Equal(1, this.repositorySavesCount);
            Assert.Equal(this.chain.Tip.Height, this.repositoryTotalBlocksSaved);

            // Present a new longer chain that will reorg the repository.
            this.chain.SetTip(savedHeader);
            this.consensusTip = this.chain.Tip;

            for (int i = 451; i <= this.chain.Height; i++)
                this.blockStore.AddToPending(new BlockPair(new Block(), this.chain.GetBlock(i)));

            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);
            
            // Make sure chain is saved.
            Assert.Equal(2, this.repositorySavesCount);
            Assert.Equal(this.chain.Tip.Height + alternativeBlocks.Count, this.repositoryTotalBlocksSaved);
            Assert.Equal(alternativeBlocks.Count, this.repositoryTotalBlocksDeleted);

            // Dispose block store.
            this.nodeLifetime.StopApplication();
            this.blockStore.Dispose();
        }
    }

    /// <summary>Extensions methos for using reflection to get / set member values.</summary>
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Gets the public or private member using reflection.
        /// </summary>
        /// <param name="obj">The source target.</param>
        /// <param name="memberName">Name of the field or property.</param>
        /// <returns>the value of member</returns>
        public static object GetMemberValue(this object obj, string memberName)
        {
            var memInf = GetMemberInfo(obj, memberName);

            if (memInf == null)
                throw new System.Exception("memberName");

            if (memInf is PropertyInfo)
                return memInf.As<PropertyInfo>().GetValue(obj, null);

            if (memInf is FieldInfo)
                return memInf.As<FieldInfo>().GetValue(obj);

            throw new System.Exception();
        }

        /// <summary>
        /// Gets the member info.
        /// </summary>
        /// <param name="obj">Source object.</param>
        /// <param name="memberName">Name of member.</param>
        /// <returns>Instanse of MemberInfo corresponsing to member.</returns>
        private static MemberInfo GetMemberInfo(object obj, string memberName)
        {
            var prps = new List<PropertyInfo>();

            prps.Add(obj.GetType().GetProperty(memberName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy));
            prps = Enumerable.ToList(Enumerable.Where(prps, i => !ReferenceEquals(i, null)));
            if (prps.Count != 0)
                return prps[0];

            var flds = new List<FieldInfo>();

            flds.Add(obj.GetType().GetField(memberName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy));

            // To add more types of properties.
            flds = Enumerable.ToList(Enumerable.Where(flds, i => !ReferenceEquals(i, null)));

            if (flds.Count != 0)
                return flds[0];

            return null;
        }

        [System.Diagnostics.DebuggerHidden]
        private static T As<T>(this object obj)
        {
            return (T)obj;
        }
    }
}
