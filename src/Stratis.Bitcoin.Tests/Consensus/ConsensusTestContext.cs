using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;
using static Stratis.Bitcoin.Tests.Consensus.ChainedHeaderTreeTest;

namespace Stratis.Bitcoin.Tests.Consensus
{
    public class TestContext
    {
        public Mock<IHeaderValidator> HeaderValidator { get; }

        public Network Network = KnownNetworks.RegTest;

        public Mock<IChainState> ChainState = new Mock<IChainState>();
        internal ChainedHeaderTree ChainedHeaderTree;
        public Mock<ICheckpoints> Checkpoints = new Mock<ICheckpoints>();
        public ConsensusSettings ConsensusSettings = new ConsensusSettings(new NodeSettings(KnownNetworks.RegTest));
        public IConsensusManager ConsensusManager;
        public readonly Mock<IConsensusRuleEngine> ConsensusRulesEngine = new Mock<IConsensusRuleEngine>();
        public Mock<IFinalizedBlockInfoRepository> FinalizedBlockMock = new Mock<IFinalizedBlockInfoRepository>();

        public readonly Mock<IInitialBlockDownloadState> ibdState = new Mock<IInitialBlockDownloadState>();
        internal ChainedHeader InitialChainTip;
        public Mock<IIntegrityValidator> IntegrityValidator = new Mock<IIntegrityValidator>();
        public readonly IPartialValidator PartialValidation;
        public readonly IFullValidator FullValidation;
        public readonly Mock<IPeerBanning> PeerBanning = new Mock<IPeerBanning>();

        private static int nonceValue;
        public Mock<ISignals> Signals = new Mock<ISignals>();

        public TestContext()
        {
            var chain = new ConcurrentChain(this.Network);
            var extendedLoggerFactory = new ExtendedLoggerFactory();
            var dateTimeProvider = new DateTimeProvider();
            var hashStore = new InvalidBlockHashStore(dateTimeProvider);
            var powConsensusRulesEngine = new PowConsensusRuleEngine(this.Network, extendedLoggerFactory, dateTimeProvider, chain,
                new NodeDeployments(this.Network, chain), this.ConsensusSettings, this.Checkpoints.Object, new Mock<ICoinView>().Object,
                this.ChainState.Object, hashStore, new NodeStats(dateTimeProvider));

            this.PartialValidation = new PartialValidator(powConsensusRulesEngine, extendedLoggerFactory);
            this.FullValidation = new FullValidator(powConsensusRulesEngine, extendedLoggerFactory);
            this.HeaderValidator = new Mock<IHeaderValidator>();
            this.HeaderValidator.Setup(hv => hv.ValidateHeader(It.IsAny<ChainedHeader>())).Returns(new ValidationContext());

            this.ChainedHeaderTree = new ChainedHeaderTree(
                this.Network,
                extendedLoggerFactory,
                this.HeaderValidator.Object,
                this.Checkpoints.Object,
                this.ChainState.Object,
                this.FinalizedBlockMock.Object,
                this.ConsensusSettings,
                hashStore);

            this.ConsensusManager = CreateConsensusManager();
        }

        public Block CreateBlock(ChainedHeader previous)
        {
            Block block = this.Network.CreateBlock();
            block.AddTransaction(this.Network.CreateTransaction());
            block.AddTransaction(this.Network.CreateTransaction());
            block.Transactions[0].AddInput(new TxIn(Script.Empty));
            block.Transactions[0].AddOutput(Money.COIN + 10, Script.Empty);
            block.GetSerializedSize();
            block.UpdateMerkleRoot();

            block.Header.HashPrevBlock = previous.HashBlock;

            return block;
        }

        private IConsensusManager CreateConsensusManager()
        {
            this.ConsensusManager = ConsensusManagerHelper.CreateConsensusManager(this.Network);

            return this.ConsensusManager;
        }

        internal Target ChangeDifficulty(ChainedHeader header, int difficultyAdjustmentDivisor)
        {
            var newTarget = header.Header.Bits.ToBigInteger();
            newTarget = newTarget.Divide(NBitcoin.BouncyCastle.Math.BigInteger.ValueOf(difficultyAdjustmentDivisor));
            return new Target(newTarget);
        }

        public void SetupCheckpoints(params CheckpointFixture[] checkpoints)
        {
            if (checkpoints.GroupBy(h => h.Height).Any(g => g.Count() > 1))
                throw new ArgumentException("Checkpoint heights must be unique.");

            if (checkpoints.Any(h => h.Height < 0))
                throw new ArgumentException("Checkpoint heights cannot be negative.");

            foreach (CheckpointFixture checkpoint in checkpoints.OrderBy(h => h.Height))
            {
                var checkpointInfo = new CheckpointInfo(checkpoint.Header.GetHash());
                this.Checkpoints
                    .Setup(c => c.GetCheckpoint(checkpoint.Height))
                    .Returns(checkpointInfo);
            }

            this.Checkpoints
                .Setup(c => c.GetCheckpoint(It.IsNotIn(checkpoints.Select(h => h.Height))))
                .Returns((CheckpointInfo)null);

            this.Checkpoints
                .Setup(c => c.GetLastCheckpointHeight())
                .Returns(checkpoints.OrderBy(h => h.Height).Last().Height);
        }

        public ChainedHeader ExtendAChain(
            int count,
            ChainedHeader chainedHeader = null,
            int difficultyAdjustmentDivisor = 1,
            bool assignBlocks = true,
            ValidationState? validationState = null)
        {
            if (difficultyAdjustmentDivisor == 0)
                throw new ArgumentException("Divisor cannot be 0");

            ChainedHeader previousHeader = chainedHeader ?? new ChainedHeader(this.Network.GetGenesis().Header, this.Network.GenesisHash, 0);

            for (int i = 0; i < count; i++)
            {
                BlockHeader header = this.Network.Consensus.ConsensusFactory.CreateBlockHeader();
                header.HashPrevBlock = previousHeader.HashBlock;
                header.Bits = difficultyAdjustmentDivisor == 1
                                    ? previousHeader.Header.Bits
                                    : this.ChangeDifficulty(previousHeader, difficultyAdjustmentDivisor);
                header.Nonce = (uint)Interlocked.Increment(ref nonceValue);

                var newHeader = new ChainedHeader(header, header.GetHash(), previousHeader);

                if (validationState.HasValue)
                    newHeader.BlockValidationState = validationState.Value;

                if (assignBlocks)
                {
                    Block block = this.Network.CreateBlock();
                    block.GetSerializedSize();
                    newHeader.Block = block;
                }

                previousHeader = newHeader;
            }

            return previousHeader;
        }

        public Block CreateBlock()
        {
            Block block = this.Network.CreateBlock();
            block.GetSerializedSize();
            return block;
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

        public bool NoDownloadRequested(ConnectNewHeadersResult connectNewHeadersResult)
        {
            Assert.NotNull(connectNewHeadersResult);

            return (connectNewHeadersResult.DownloadTo == null)
                   && (connectNewHeadersResult.DownloadFrom == null);
        }

        /// <summary>
        /// Initial setup for tests 18-20, 28.
        /// Chain header tree setup. Initial chain has 4 headers.
        /// SetUp:
        ///                        =8d=9d=10d
        ///                   6a=7a=8a=9a
        /// GENESIS=1=2=3=4=5=
        ///                   6b=7b=8b=9b
        ///             3c=4c=5c
        /// </summary>
        /// <param name="cht">ChainHeaderTree.</param>
        /// <param name="initialChainTip">Initial chain tip.</param>
        internal void SetupPeersForTest(ChainedHeaderTree cht, ChainedHeader initialChainTip)
        {
            int peerAExtension = 4;
            int peerBExtension = 4;
            int peerCExtension = 3;
            int peerDExtension = 3;

            ChainedHeader chainATip = this.ExtendAChain(peerAExtension, initialChainTip); // i.e. (h1=h2=h3=h4=h5)=6a=7a=8a=9a
            ChainedHeader chainBTip = this.ExtendAChain(peerBExtension, initialChainTip); // i.e. (h1=h2=h3=h4=h5)=6b=7b=8b=9b
            ChainedHeader chainCTip = this.ExtendAChain(peerCExtension, initialChainTip.GetAncestor(2)); // i.e. (h1=h2)=3c=4c=5c
            ChainedHeader chainDTip = this.ExtendAChain(peerDExtension, chainATip.GetAncestor(7)); // i.e. ((h1=h2=h3=h4=h5)=6a=7a)=8d=9d=10d

            List<BlockHeader> peerABlockHeaders = this.ChainedHeaderToList(chainATip, chainATip.Height);
            List<BlockHeader> peerBBlockHeaders = this.ChainedHeaderToList(chainBTip, chainBTip.Height);
            List<BlockHeader> peerCBlockHeaders = this.ChainedHeaderToList(chainCTip, chainCTip.Height);
            List<BlockHeader> peerDBlockHeaders = this.ChainedHeaderToList(chainDTip, chainDTip.Height);

            cht.ConnectNewHeaders(0, peerABlockHeaders);
            cht.ConnectNewHeaders(1, peerBBlockHeaders);
            cht.ConnectNewHeaders(2, peerCBlockHeaders);
            cht.ConnectNewHeaders(3, peerDBlockHeaders);
        }

        internal void SwitchToChain(ChainedHeaderTree cht, ChainedHeader chainTip, ChainedHeader consumedHeader, int extensionSize)
        {
            ChainedHeader[] consumedHeaders = consumedHeader.ToArray(extensionSize);

            for (int i = 0; i < extensionSize; i++)
            {
                ChainedHeader currentConsumedCh = consumedHeaders[i];
                cht.BlockDataDownloaded(currentConsumedCh, chainTip.GetAncestor(currentConsumedCh.Height).Block);
                cht.PartialValidationSucceeded(currentConsumedCh, out bool fullValidationRequired);
                cht.ConsensusTipChanged(currentConsumedCh);
            }
        }
    }
}
