using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.ProvenBlockHeaders
{
    public class ProvenBlockHeaderStoreTests : LogsTestBase
    {
        private readonly Network network = KnownNetworks.StratisTest;
        private readonly Mock<IConsensusManager> consensusManager;
        private readonly ConcurrentChain concurrentChain;
        private readonly ProvenBlockHeaderStore provenBlockHeaderStore;
        private IProvenBlockHeaderRepository provenBlockHeaderRepository;
        private readonly IChainState chainState;
        private readonly Mock<INodeLifetime> nodeLifetime;
        private readonly string Folder;
        private readonly NodeStats nodeStats;

        public ProvenBlockHeaderStoreTests() : base(KnownNetworks.StratisTest)
        {
            this.consensusManager = new Mock<IConsensusManager>();
            this.concurrentChain = this.GenerateChainWithHeight(3);
            this.chainState = new ChainState();
            this.nodeLifetime = new Mock<INodeLifetime>();
            this.nodeStats = new NodeStats(DateTimeProvider.Default);


            this.Folder = CreateTestDir(this);

            this.provenBlockHeaderRepository = new ProvenBlockHeaderRepository(this.network, this.Folder, this.LoggerFactory.Object);

            this.provenBlockHeaderStore = new ProvenBlockHeaderStore(
                this.concurrentChain, DateTimeProvider.Default, this.LoggerFactory.Object,
                this.provenBlockHeaderRepository, this.nodeLifetime.Object, this.chainState, this.nodeStats);
        }

        [Fact(Skip = "Fix as part of the caching layer work")]
        public async Task InitialiseStoreBySettingChainHeaderAsync()
        {
            ChainedHeader chainedHeader = this.concurrentChain.Tip.Previous.Previous;

            await this.provenBlockHeaderStore.InitializeAsync().ConfigureAwait(false);

            chainedHeader.Should().BeSameAs(this.chainState.BlockStoreTip);
        }

        [Fact]
        public async Task InitialiseStoreToGenesisChainHeaderAsync()
        {
            ChainedHeader chainedHeader = this.concurrentChain.Genesis;

            await this.provenBlockHeaderStore.InitializeAsync().ConfigureAwait(false);

            chainedHeader.HashBlock.Should().Be(this.network.GetGenesis().GetHash());
        }

        [Fact(Skip = "Fix as part of the caching layer work")]
        public async Task LoadItemsAsync()
        {
            // Put 4 items to in the repository - items created in the constructor.
            var inItems = new List<ProvenBlockHeader>();

            var itemCounter = 0;

            var provenHeaderMock = CreateNewProvenBlockHeaderMock();

            ChainedHeader chainedHeader = this.concurrentChain.Tip;

            while(chainedHeader != null)
            {
                inItems.Add(provenHeaderMock);

                chainedHeader = chainedHeader.Previous;

                itemCounter++;
            }

            await this.provenBlockHeaderRepository.PutAsync(inItems, new HashHeightPair());

            // Then load them.
            using (IProvenBlockHeaderStore store = this.SetupStore(this.Folder))
            {
                var outItems = await store.GetAsync(0, itemCounter).ConfigureAwait(false);

                outItems.Count.Should().Be(itemCounter);
                
                foreach(var item in outItems)
                {
                    item.Should().BeSameAs(provenHeaderMock);
                }
            }
        }

        private IProvenBlockHeaderStore SetupStore(string folder)
        {
            return new ProvenBlockHeaderStore(
                this.concurrentChain, DateTimeProvider.Default,
                this.LoggerFactory.Object, this.provenBlockHeaderRepository,
                this.nodeLifetime.Object, this.chainState, new NodeStats(DateTimeProvider.Default));
        }

        private ConcurrentChain GenerateChainWithHeight(int blockAmount)
        {
            var chain = new ConcurrentChain(this.network);
            uint nonce = RandomUtils.GetUInt32();
            uint256 prevBlockHash = chain.Genesis.HashBlock;
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = this.network.Consensus.ConsensusFactory.CreateBlock();
                block.AddTransaction(this.network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i));
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;
                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
            }

            return chain;
        }
    }
}
