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

        public ProvenBlockHeaderStoreTests() : base(KnownNetworks.StratisTest)
        {
            this.consensusManager = new Mock<IConsensusManager>();
            this.concurrentChain = this.GenerateChainWithHeight(3);
            this.chainState = new ChainState();
            this.nodeLifetime = new Mock<INodeLifetime>();

            this.Folder = CreateTestDir(this);

            this.provenBlockHeaderRepository = new ProvenBlockHeaderRepository(this.network,
                this.Folder, DateTimeProvider.Default, this.LoggerFactory.Object, 
                new NodeStats(DateTimeProvider.Default));

            this.provenBlockHeaderStore = new ProvenBlockHeaderStore(this.network,
                this.concurrentChain, DateTimeProvider.Default, this.LoggerFactory.Object,
                this.provenBlockHeaderRepository, this.nodeLifetime.Object, this.chainState);
        }

        [Fact]
        public async Task InitialiseStoreBySettingChainHeaderAsync()
        {
            ChainedHeader chainedHeader = this.concurrentChain.Tip.Previous.Previous;

            await this.provenBlockHeaderStore.InitializeAsync(chainedHeader.HashBlock).ConfigureAwait(false);

            chainedHeader.Should().BeSameAs(this.chainState.BlockStoreTip);
        }

        [Fact]
        public async Task InitialiseStoreToGenesisChainHeaderAsync()
        {
            ChainedHeader chainedHeader = this.concurrentChain.Genesis;

            await this.provenBlockHeaderStore.InitializeAsync().ConfigureAwait(false);

            chainedHeader.HashBlock.Should().Be(this.network.GetGenesis().GetHash());
        }


        [Fact]
        public void LoadItems()
        {
            // Put 4 items to in the repository.
            var items = new List<StakeItem>();
            var itemCounter = 0;

            ChainedHeader chainedHeader = this.concurrentChain.Tip;
            while(chainedHeader != null)
            {
                items.Add(new StakeItem
                {
                    BlockId = chainedHeader.HashBlock,
                    Height = chainedHeader.Height,
                    ProvenBlockHeader = CreateNewProvenBlockHeaderMock()
                });

                chainedHeader = chainedHeader.Previous;

                itemCounter++;
            }

            Task task = this.provenBlockHeaderRepository.PutAsync(items);
            task.Wait();

            // Then load them.
            using (IProvenBlockHeaderStore store = this.SetupStore(this.Network, this.Folder))
            {
                task = store.LoadAsync();
                task.Wait();

                items.Count.Should().Be(itemCounter);
                items.Should().BeOfType<List<StakeItem>>();
                items.ForEach(i => i.ProvenBlockHeader.Should().NotBeNull());
                items.ForEach(i => i.InStore.Should().BeTrue());
            }
        }

        private IProvenBlockHeaderStore SetupStore(Network network, string folder)
        {
            var store = new ProvenBlockHeaderStore(
                network, this.concurrentChain, DateTimeProvider.Default, 
                this.LoggerFactory.Object, this.provenBlockHeaderRepository, 
                this.nodeLifetime.Object, this.chainState);

            return store;
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
