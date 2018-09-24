using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NBitcoin;
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
        private IProvenBlockHeaderRepository provenBlockHeaderRepository;

        public ProvenBlockHeaderStoreTests() : base(KnownNetworks.StratisTest)
        {
            this.consensusManager = new Mock<IConsensusManager>();
            this.concurrentChain = this.GenerateChainWithHeight(3, this.network);
        }

        [Fact]
        public void LoadItems()
        {
            string folder = CreateTestDir(this);

            this.provenBlockHeaderRepository = new ProvenBlockHeaderRepository(this.network, folder, DateTimeProvider.Default, this.LoggerFactory.Object, new NodeStats(DateTimeProvider.Default));

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
            using (IProvenBlockHeaderStore store = this.SetupStore(this.Network, folder))
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
                network, this.concurrentChain, DateTimeProvider.Default, this.LoggerFactory.Object, this.provenBlockHeaderRepository);

            return store;
        }

        private ConcurrentChain GenerateChainWithHeight(int blockAmount, Network network)
        {
            var chain = new ConcurrentChain(network);
            uint nonce = RandomUtils.GetUInt32();
            uint256 prevBlockHash = chain.Genesis.HashBlock;
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = network.Consensus.ConsensusFactory.CreateBlock();
                block.AddTransaction(network.CreateTransaction());
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
