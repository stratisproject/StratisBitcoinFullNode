using System.Collections.Generic;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Tests.Consensus
{
    public class ConsensusManagerTests
    {
        public class TestContext
        {
            public readonly Network Network = KnownNetworks.RegTest;
            public Mock<IHeaderValidator> HeaderValidatorMock = new Mock<IHeaderValidator>();
            public Mock<IIntegrityValidator> IntegrityValidatorMock = new Mock<IIntegrityValidator>();
            public readonly Mock<IPartialValidation> PartialValidationMock = new Mock<IPartialValidation>();
            public readonly Mock<ICheckpoints> CheckpointsMock = new Mock<ICheckpoints>();
            public readonly Mock<IPeerBanning> PeerBanningMock = new Mock<IPeerBanning>();

            public readonly Mock<IChainState> ChainStateMock = new Mock<IChainState>();
            public readonly Mock<IConsensusRules> ConsensusRulesMock = new Mock<IConsensusRules>();

            public readonly Mock<IFinalizedBlockHeight> FinalizedBlockMock = new Mock<IFinalizedBlockHeight>();
            public readonly ConsensusSettings ConsensusSettings = new ConsensusSettings(new NodeSettings(KnownNetworks.RegTest));
            public readonly Mock<IInitialBlockDownloadState> ibdStateLock = new Mock<IInitialBlockDownloadState>();

            internal ChainedHeaderTree ChainedHeaderTree;

            public IConsensusManager ConsensusManager;

            internal IConsensusManager CreateConsensusManager()
            {
                this.ConsensusManager = new ConsensusManager(this.Network, new ExtendedLoggerFactory(), this.ChainStateMock.Object,
                    this.HeaderValidatorMock.Object, this.IntegrityValidatorMock.Object, this.PartialValidationMock.Object, this.CheckpointsMock.Object, this.ConsensusSettings,
                    this.ConsensusRulesMock.Object, this.FinalizedBlockMock.Object, new Bitcoin.Signals.Signals(), this.PeerBanningMock.Object, new NodeSettings(this.Network), new DateTimeProvider(), this.ibdStateLock.Object, new ConcurrentChain(this.Network));

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
    }
}