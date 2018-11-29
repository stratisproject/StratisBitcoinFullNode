using System.Collections.Generic;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Behaviors;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.ProvenBlockHeaders
{
    public sealed class ProvenHeaderConsenusManagerBehaviorTests : LogsTestBase
    {
        private readonly IChainState chainState;
        private readonly ICheckpoints checkpoints;
        private readonly ConnectionManagerSettings connectionManagerSettings;
        private readonly IConsensusManager consensusManager;
        private readonly ExtendedLoggerFactory extendedLoggerFactory;
        private readonly IInitialBlockDownloadState initialBlockDownloadState;
        private readonly IPeerBanning peerBanning;
        private readonly IProvenBlockHeaderStore provenBlockHeaderStore;

        public ProvenHeaderConsenusManagerBehaviorTests() : base(new StratisTest())
        {
            this.chainState = new Mock<IChainState>().Object;
            this.checkpoints = new Mock<ICheckpoints>().Object;
            this.connectionManagerSettings = new ConnectionManagerSettings(NodeSettings.Default(this.Network));
            this.consensusManager = new Mock<IConsensusManager>().Object;
            this.extendedLoggerFactory = new ExtendedLoggerFactory(); this.extendedLoggerFactory.AddConsoleWithFilters();
            this.initialBlockDownloadState = new Mock<IInitialBlockDownloadState>().Object;
            this.peerBanning = new Mock<IPeerBanning>().Object;
            this.provenBlockHeaderStore = new Mock<IProvenBlockHeaderStore>().Object;
        }

        [Fact]
        public void ConstructProvenHeaderPayload_Consecutive_Headers()
        {
            var provenHeaderChain = BuildChainWithProvenHeaders(10);

            var chain = new ConcurrentChain(this.Network, provenHeaderChain);

            var behavior = new ProvenHeadersConsensusManagerBehavior(chain, this.initialBlockDownloadState, this.consensusManager, this.peerBanning, this.extendedLoggerFactory, this.Network, this.chainState, this.checkpoints, this.provenBlockHeaderStore, this.connectionManagerSettings);

            ChainedHeader chainedHeader = null;

            var hashes = new List<uint256>();
            for (int i = 1; i < 5; i++)
            {
                var chainedHeaderToAdd = chain.GetBlock(i);
                hashes.Add(chainedHeaderToAdd.HashBlock);
            }

            hashes.Reverse();

            var blockLocator = new BlockLocator
            {
                Blocks = hashes
            };

            var payload = (ProvenHeadersPayload)behavior.ConstructHeadersPayload(new GetProvenHeadersPayload(blockLocator), out chainedHeader);
            Assert.Equal(5, payload.Headers.Count);
        }

        [Fact]
        public void ConstructProvenHeaderPayload_NonConsecutive_Headers()
        {
            ChainedHeader provenHeaderChain = ChainedHeadersHelper.CreateGenesisChainedHeader(this.Network);

            var itemsToReturnInMock = new List<PosBlockHeader>();

            for (int i = 1; i < 10; i++)
            {
                PosBlock block = this.CreatePosBlockMock();

                PosBlockHeader header = null;

                if (i == 7)
                    header = (PosBlockHeader)((PosConsensusFactory)this.Network.Consensus.ConsensusFactory).CreateBlockHeader();
                else
                    header = ((PosConsensusFactory)this.Network.Consensus.ConsensusFactory).CreateProvenBlockHeader(block);

                header.Nonce = RandomUtils.GetUInt32();
                header.HashPrevBlock = provenHeaderChain.HashBlock;
                header.Bits = Target.Difficulty1;

                ChainedHeader prevHeader = provenHeaderChain;
                provenHeaderChain = new ChainedHeader(header, header.GetHash(), i);

                provenHeaderChain.SetPrivatePropertyValue("Previous", prevHeader);

                prevHeader.Next.Add(provenHeaderChain);

                if (i >= 5)
                    itemsToReturnInMock.Add(header);
            }

            var chain = new ConcurrentChain(this.Network, provenHeaderChain);

            var provenBlockHeaderStoreWithInvalidPrevious = new Mock<IProvenBlockHeaderStore>();

            provenBlockHeaderStoreWithInvalidPrevious.SetupSequence(p => p.GetAsync(It.IsAny<int>()))
                .ReturnsAsync((ProvenBlockHeader)itemsToReturnInMock[0]) //5
                .ReturnsAsync((ProvenBlockHeader)itemsToReturnInMock[1]) //6
                .ReturnsAsync(new ProvenBlockHeader(CreatePosBlockMock())); //7

            var behavior = new ProvenHeadersConsensusManagerBehavior(chain, this.initialBlockDownloadState, this.consensusManager, this.peerBanning, this.extendedLoggerFactory, this.Network, this.chainState, this.checkpoints, provenBlockHeaderStoreWithInvalidPrevious.Object, this.connectionManagerSettings);

            ChainedHeader chainedHeader = null;

            var hashes = new List<uint256>();
            for (int i = 1; i < 5; i++)
            {
                var chainedHeaderToAdd = chain.GetBlock(i);
                hashes.Add(chainedHeaderToAdd.HashBlock);
            }

            hashes.Reverse();

            var blockLocator = new BlockLocator
            {
                Blocks = hashes
            };

            var payload = (ProvenHeadersPayload)behavior.ConstructHeadersPayload(new GetProvenHeadersPayload(blockLocator), out chainedHeader);
            Assert.Equal(2, payload.Headers.Count);
            Assert.Equal(itemsToReturnInMock[0].GetHash(), payload.Headers[0].GetHash());
            Assert.Equal(itemsToReturnInMock[1].GetHash(), payload.Headers[1].GetHash());
        }
    }
}
