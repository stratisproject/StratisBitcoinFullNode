using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Tests.Consensus
{
    public class ConsensusManagerTests
    {
        public class TestContext
        {
            public Network Network = Network.RegTest;
            public Mock<IBlockValidator> ChainedHeaderValidatorMock = new Mock<IBlockValidator>();
            public Mock<ICheckpoints> CheckpointsMock = new Mock<ICheckpoints>();
            public Mock<IBlockPuller> BlockPullerMock = new Mock<IBlockPuller>();

            public Mock<IChainState> ChainStateMock = new Mock<IChainState>();
            public Mock<IConsensusRules> ConsensusRulesMock = new Mock<IConsensusRules>();
            public Mock<IConnectionManager> ConnectionManagerMock = new Mock<IConnectionManager>();

            public Mock<IFinalizedBlockHeight> FinalizedBlockMock = new Mock<IFinalizedBlockHeight>();
            public ConsensusSettings ConsensusSettings = new ConsensusSettings(new NodeSettings(Network.RegTest));

            internal ChainedHeaderTree ChainedHeaderTree;

            public ConsensusManager ConsensusManager;

            internal ConsensusManager CreateConsensusManager()
            {
                this.ConsensusManager = new ConsensusManager(this.Network, new ExtendedLoggerFactory(), this.ChainStateMock.Object, 
                    this.ChainedHeaderValidatorMock.Object, this.CheckpointsMock.Object, this.ConsensusSettings, this.BlockPullerMock.Object,
                    this.ConsensusRulesMock.Object, this.FinalizedBlockMock.Object, this.ConnectionManagerMock.Object );

                this.ChainedHeaderTree = this.ConsensusManager.GetMemberValue("chainedHeaderTree") as ChainedHeaderTree;

                return this.ConsensusManager;
            }

            public ChainedHeader ExtendAChain(int count, ChainedHeader chainedHeader = null)
            {
                ChainedHeader previousHeader = chainedHeader ?? new ChainedHeader(this.Network.GetGenesis().Header, this.Network.GenesisHash, 0);

                for (int i = 0; i < count; i++)
                {
                    BlockHeader header = this.Network.Consensus.ConsensusFactory.CreateBlockHeader();
                    header.HashPrevBlock = previousHeader.HashBlock;
                    header.Bits = previousHeader.Header.Bits - 1000; // just increase difficulty.
                    var newHeader = new ChainedHeader(header, header.GetHash(), previousHeader);
                    previousHeader = newHeader;
                }

                return previousHeader;
            }

            public List<BlockHeader> ChainedHeaderToList(ChainedHeader chainedHeader, int count)
            {
                var list = new List<BlockHeader>();

                ChainedHeader current = chainedHeader;

                for (int i = 0; i < count; i++)
                {
                    list.Add(current.Header);
                    current = current.Previous;
                }

                list.Reverse();

                return list;
            }
        }

        [Fact]
        public void HeadersPresented_HeadersAreAlreadyPresented_ShouldNotAddNewHeaders()
        {
            var testContext = new TestContext();
            ConsensusManager consensusManager = testContext.CreateConsensusManager();
            ChainedHeader chainTip = testContext.ExtendAChain(10);

            testContext.ConsensusRulesMock.Setup(s => s.GetBlockHashAsync()).Returns(Task.FromResult(chainTip.HashBlock));

            consensusManager.InitializeAsync(chainTip).Wait();

            List<BlockHeader> listOfExistingHeaders = testContext.ChainedHeaderToList(chainTip, 10);

            ChainedHeader chainedHeader = consensusManager.HeadersPresented(1, listOfExistingHeaders);

            Assert.Equal(10, consensusManager.Tip.Height);
            Assert.Equal(chainTip, chainedHeader);
        }
    }
}