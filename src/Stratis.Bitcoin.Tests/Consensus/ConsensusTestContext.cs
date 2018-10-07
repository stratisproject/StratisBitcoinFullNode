﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
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

        // public Mock<IChainState> ChainState = new Mock<IChainState>();
        internal ChainedHeaderTree ChainedHeaderTree;

        // private Mock<IFinalizedBlockInfo> finalizedBlockInfo;
        // private Mock<INodeStats> nodeStats;
        private INodeStats nodeStats;
        private Mock<IInitialBlockDownloadState> ibd;
        private Mock<IBlockPuller> blockPuller;
        private Mock<IBlockStore> blockStore;
        // public Mock<ICheckpoints> Checkpoints = new Mock<ICheckpoints>();
        // private ICheckpoints checkpoints;
        private Mock<ICheckpoints> checkpoints = new Mock<ICheckpoints>();

        public TestConsensusManager ConsensusManager;
        // public readonly Mock<IConsensusRuleEngine> ConsensusRulesEngine = new Mock<IConsensusRuleEngine>();
        public Mock<IFinalizedBlockInfoRepository> FinalizedBlockMock = new Mock<IFinalizedBlockInfoRepository>();

        public readonly Mock<IInitialBlockDownloadState> ibdState = new Mock<IInitialBlockDownloadState>();
        internal ChainedHeader InitialChainTip;
        public Mock<IIntegrityValidator> IntegrityValidator = new Mock<IIntegrityValidator>();
        public readonly IPartialValidator PartialValidation;
        public readonly IFullValidator FullValidation;
        // public readonly Mock<IPeerBanning> PeerBanning = new Mock<IPeerBanning>();
        private IPeerBanning peerBanning;
        private IConnectionManager connectionManager;

        private static int nonceValue;

        // public Mock<ISignals> Signals = new Mock<ISignals>();
        private ConcurrentChain chain;
        // private ExtendedLoggerFactory extendedLoggerFactory;
        private DateTimeProvider dateTimeProvider;
        private InvalidBlockHashStore hashStore;
        private NodeSettings nodeSettings;
        private ILoggerFactory loggerFactory;
        private IRuleRegistration ruleRegistration;

        // todo: merge
        public ConsensusSettings ConsensusSettings = new ConsensusSettings(new NodeSettings(KnownNetworks.RegTest));
        private ConsensusSettings consensusSettings;
        private INetworkPeerFactory networkPeerFactory;
        public Mock<IChainState> ChainState;

        private readonly IConsensusRuleEngine consensusRules;
        public readonly TestInMemoryCoinView coinView;
        // private readonly IConsensusRuleEngine powConsensusRulesEngine;
        private NodeDeployments deployments;
        private ISelfEndpointTracker selfEndpointTracker;
        private INodeLifetime nodeLifetime;

        public TestContext()
        {
            this.chain = new ConcurrentChain(this.Network);
            // this.extendedLoggerFactory = new ExtendedLoggerFactory();
            this.dateTimeProvider = new DateTimeProvider();
            this.hashStore = new InvalidBlockHashStore(this.dateTimeProvider);

            // todo:merge
            //  new Mock<ICoinView>().Object
            this.coinView = new TestInMemoryCoinView(this.chain.Tip.HashBlock);
            this.HeaderValidator = new Mock<IHeaderValidator>();
            this.HeaderValidator.Setup(hv => hv.ValidateHeader(It.IsAny<ChainedHeader>())).Returns(new ValidationContext());
            
            this.nodeLifetime = new NodeLifetime();            
            // this.nodeStats = new Mock<INodeStats>();
            this.ibd = new Mock<IInitialBlockDownloadState>();
            this.blockPuller = new Mock<IBlockPuller>();
            this.blockStore = new Mock<IBlockStore>();
            // this.checkpoints = new Checkpoints();
            this.checkpoints = new Mock<ICheckpoints>();
            // this.chainState = new ChainState();
            this.ChainState = new Mock<IChainState>();
            this.nodeStats = new NodeStats(this.dateTimeProvider);


            string[] param = new string[] { };
            this.nodeSettings = new NodeSettings(this.Network, args: param);


            // if (loggerFactory == null)
            this.loggerFactory = this.nodeSettings.LoggerFactory;
            // if (dateTimeProvider == null)
            //     dateTimeProvider = DateTimeProvider.Default;

            this.selfEndpointTracker = new SelfEndpointTracker(this.loggerFactory);
            this.ChainedHeaderTree = new ChainedHeaderTree(
              this.Network,
              this.loggerFactory,
              this.HeaderValidator.Object,
              // this.Checkpoints.Object,
              this.checkpoints.Object,
              // this.ChainState.Object,
              this.ChainState.Object,
              this.FinalizedBlockMock.Object,
              this.ConsensusSettings,
              this.hashStore);

            this.Network.Consensus.Options = new ConsensusOptions();

            this.ruleRegistration = new FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration();
            this.ruleRegistration.RegisterRules(this.Network.Consensus);

            // Dont check PoW of a header in this test.
            this.Network.Consensus.HeaderValidationRules.RemoveAll(x => x.GetType() == typeof(CheckDifficultyPowRule));

            this.consensusSettings = new ConsensusSettings(this.nodeSettings);

            // if (chain == null)
            //     chain = new ConcurrentChain(network);

            // inMemoryCoinView = new InMemoryCoinView(chain.Tip.HashBlock);

            this.networkPeerFactory = new NetworkPeerFactory(this.Network,
                this.dateTimeProvider,
                this.loggerFactory, new PayloadProvider().DiscoverPayloads(),
                this.selfEndpointTracker,
                this.ibd.Object,
                new ConnectionManagerSettings());

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, this.nodeSettings.DataFolder, this.loggerFactory, this.selfEndpointTracker);
            var peerDiscovery = new PeerDiscovery(new AsyncLoopFactory(this.loggerFactory), this.loggerFactory, this.Network, this.networkPeerFactory, this.nodeLifetime, this.nodeSettings, peerAddressManager);
            var connectionSettings = new ConnectionManagerSettings(this.nodeSettings);            
            
            this.connectionManager = new ConnectionManager(this.dateTimeProvider, this.loggerFactory, this.Network, this.networkPeerFactory, this.nodeSettings,
                this.nodeLifetime, new NetworkPeerConnectionParameters(), peerAddressManager, new IPeerConnector[] { },
                peerDiscovery, this.selfEndpointTracker, connectionSettings, new VersionProvider(), this.nodeStats);


            // this.chainState = new ChainState();


            // todo: merge

            this.deployments = new NodeDeployments(this.Network, this.chain);

            // this.powConsensusRulesEngine = new PowConsensusRuleEngine(this.Network, this.extendedLoggerFactory, this.dateTimeProvider, this.chain,
            //     new NodeDeployments(this.Network, this.chain), this.ConsensusSettings, this.Checkpoints.Object, this.coinView,
            //     this.ChainState.Object, this.hashStore, new NodeStats(this.dateTimeProvider));

            this.consensusRules = new PowConsensusRuleEngine(this.Network, this.loggerFactory, this.dateTimeProvider, this.chain, this.deployments, this.ConsensusSettings,
                     this.checkpoints.Object, this.coinView, this.ChainState.Object, this.hashStore, this.nodeStats);

            this.consensusRules.Register();
            // this.powConsensusRulesEngine.Register();

            // new HeaderValidator(this.consensusRules, this.loggerFactory)
            var tree = new ChainedHeaderTree(this.Network, this.loggerFactory, this.HeaderValidator.Object , this.checkpoints.Object,
                this.ChainState.Object, this.FinalizedBlockMock.Object, this.consensusSettings, this.hashStore);


            // new PartialValidator(powConsensusRulesEngine, loggerFactory)
            // new FullValidator(powConsensusRulesEngine, loggerFactory)


            this.PartialValidation = new PartialValidator(this.consensusRules, this.loggerFactory);
            this.FullValidation = new FullValidator(this.consensusRules, this.loggerFactory);
            this.peerBanning = new PeerBanning(this.connectionManager, this.loggerFactory, this.dateTimeProvider, peerAddressManager);

            this.ConsensusManager = new TestConsensusManager(tree, this.Network, this.loggerFactory, this.ChainState.Object, new IntegrityValidator(this.consensusRules, this.loggerFactory),
                this.PartialValidation, this.FullValidation, this.consensusRules,
                this.FinalizedBlockMock.Object, new Stratis.Bitcoin.Signals.Signals(), this.peerBanning, this.ibd.Object, this.chain,
                this.blockPuller.Object, this.blockStore.Object, this.connectionManager, this.nodeStats, this.nodeLifetime);

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

        // private IConsensusManager CreateConsensusManager()
        // {
        //     this.ConsensusManager = ConsensusManagerHelper.CreateConsensusManager(this.Network);
        //     return this.ConsensusManager;
        // }

        internal Target ChangeDifficulty(ChainedHeader header, int difficultyAdjustmentDivisor)
        {
            NBitcoin.BouncyCastle.Math.BigInteger newTarget = header.Header.Bits.ToBigInteger();
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
                this.checkpoints
                    .Setup(c => c.GetCheckpoint(checkpoint.Height))
                    .Returns(checkpointInfo);
            }

            this.checkpoints
                .Setup(c => c.GetCheckpoint(It.IsNotIn(checkpoints.Select(h => h.Height))))
                .Returns((CheckpointInfo)null);

            this.checkpoints
                .Setup(c => c.GetLastCheckpointHeight())
                .Returns(checkpoints.OrderBy(h => h.Height).Last().Height);
        }

        public ChainedHeader ExtendAChain(
            int count,
            ChainedHeader chainedHeader = null,
            int difficultyAdjustmentDivisor = 1,
            bool assignBlocks = true,
            ValidationState? validationState = null, 
            int? avgBlockSize = null)
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
                    Block block = this.Network.Consensus.ConsensusFactory.CreateBlock();
                    block.GetSerializedSize();

                    if (avgBlockSize.HasValue)
                    {                        
                        var transaction = new Transaction();
                        transaction.Outputs.Add(new TxOut(new Money(10000000000), new Script()));
                        block.Transactions.Add(transaction);

                        int blockWeight = block.GetSerializedSize();

                        int requiredScriptWeight = avgBlockSize.Value - blockWeight - 2;
                        block.Transactions[0].Outputs.Clear();
                        // generate nonsense script with required bytes to reach required weight.
                        Script script = Script.FromBytesUnsafe(new string('A', requiredScriptWeight).Select(c => (byte)c).ToArray());
                        transaction.Outputs.Add(new TxOut(new Money(10000000000), script));

                        block.GetSerializedSize();

                        if (block.BlockSize != avgBlockSize.Value)
                        {
                            throw new Exception("Unable to generate block with expected size.");
                        }
                    }


                    newHeader.Block = block;
                }

                previousHeader = newHeader;
            }

            return previousHeader;
        }

        public Block CreateBlock()
        {
            Block block = this.Network.Consensus.ConsensusFactory.CreateBlock();
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

        internal void SetupAverageBlockSize(int amount)
        {
            this.blockPuller.Setup(b => b.GetAverageBlockSizeBytes())
                .Returns(amount);            
        }


        internal Mock<INetworkPeer> GetNetworkPeerWithConnection()
        {
            var networkPeer = new Mock<INetworkPeer>();

            var connection = new NetworkPeerConnection(this.Network, networkPeer.Object, new TcpClient(), 0, (message, token) => Task.CompletedTask,
            this.dateTimeProvider, this.loggerFactory, new PayloadProvider().DiscoverPayloads());
            networkPeer.Setup(n => n.Connection)
                .Returns(connection);

            return networkPeer;
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
