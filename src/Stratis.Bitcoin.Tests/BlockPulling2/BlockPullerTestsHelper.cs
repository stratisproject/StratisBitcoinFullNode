using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling2;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Tests.BlockPulling2
{
    public class BlockPullerTestsHelper
    {
        public readonly ExtendedBlockPuller Puller;

        /// <summary>
        /// Headers to blocks provided through the callback which is called by the
        /// puller when blocks are delivered or failed to be delivered.
        /// </summary>
        public readonly Dictionary<uint256, Block> CallbacksCalled;

        public readonly ChainState ChainState;

        private int currentPeerId = 0;
        private readonly ILoggerFactory loggerFactory;
        private int currentNonce;
        
        public BlockPullerTestsHelper()
        {
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
            this.currentNonce = 0;

            this.CallbacksCalled = new Dictionary<uint256, Block>();
            this.ChainState = new ChainState(new InvalidBlockHashStore(new DateTimeProvider())) {ConsensusTip = this.CreateGenesisChainedHeader()};

            this.Puller = new ExtendedBlockPuller((hash, block) => { this.CallbacksCalled.Add(hash, block); },
                this.ChainState, NodeSettings.SupportedProtocolVersion, new DateTimeProvider(), this.loggerFactory);
        }

        /// <summary>Creates a peer with extended puller behavior.</summary>
        public INetworkPeer CreatePeer(out ExtendedBlockPullerBehavior mockedBehavior, bool notSupportedVersion = false)
        {
            var peer = new Mock<INetworkPeer>();
            
            var connection = new NetworkPeerConnection(Network.StratisMain, peer.Object, new TcpClient(), this.currentPeerId, (message, token) => Task.CompletedTask,
                new DateTimeProvider(), this.loggerFactory, new PayloadProvider());

            this.currentPeerId++;
            peer.SetupGet(networkPeer => networkPeer.Connection).Returns(connection);

            var connectionParameters = new NetworkPeerConnectionParameters();
            VersionPayload version = connectionParameters.CreateVersion(new IPEndPoint(1, 1), Network.StratisMain, new DateTimeProvider().GetTimeOffset());

            if (notSupportedVersion)
                version.Version = ProtocolVersion.NOBLKS_VERSION_START;
            else
                version.Services = NetworkPeerServices.Network;

            peer.SetupGet(x => x.PeerVersion).Returns(version);
            peer.SetupGet(x => x.State).Returns(NetworkPeerState.HandShaked);
            peer.SetupGet(x => x.MessageReceived).Returns(new AsyncExecutionEvent<INetworkPeer, IncomingMessage>());

            ExtendedBlockPullerBehavior behavior = this.CreateBlockPullerBehavior();
            behavior.Attach(peer.Object);
            peer.Setup(x => x.Behavior<IBlockPullerBehavior>()).Returns(() => behavior);

            mockedBehavior = behavior;
            return peer.Object;
        }

        private ExtendedBlockPullerBehavior CreateBlockPullerBehavior()
        {
            var ibdState = new Mock<IInitialBlockDownloadState>();
            ibdState.Setup(x => x.IsInitialBlockDownload()).Returns(() => true);

            var behavior = new ExtendedBlockPullerBehavior(this.Puller, ibdState.Object, this.loggerFactory);

            return behavior;
        }

        public List<ChainedHeader> CreateConsequtiveHeaders(int count, ChainedHeader prevBlock = null)
        {
            var chainedHeaders = new List<ChainedHeader>();
            Network network = Network.StratisMain;
            
            ChainedHeader tip = prevBlock ?? this.CreateGenesisChainedHeader();
            uint256 hashPrevBlock = tip.HashBlock;

            for (int i = 0; i < count; ++i)
            {
                BlockHeader header = network.Consensus.ConsensusFactory.CreateBlockHeader();
                header.Nonce = (uint)Interlocked.Increment(ref this.currentNonce);
                header.HashPrevBlock = hashPrevBlock;
                header.Bits = Target.Difficulty1;

                var chainedHeader = new ChainedHeader(header, header.GetHash(), tip);

                hashPrevBlock = chainedHeader.HashBlock;
                tip = chainedHeader;

                chainedHeaders.Add(chainedHeader);
            }

            return chainedHeaders;
        }

        private ChainedHeader CreateGenesisChainedHeader()
        {
            return new ChainedHeader(Network.StratisMain.GetGenesis().Header, Network.StratisMain.GenesisHash, 0);
        }

        /// <summary>Creates a new block with mocked serialized size.</summary>
        public Block GenerateBlock(long size)
        {
            Block block = Network.StratisMain.Consensus.ConsensusFactory.CreateBlock();

            block.SetPrivatePropertyValue("BlockSize", size);

            return block;
        }

        public ChainedHeader CreateChainedHeader()
        {
            return this.CreateConsequtiveHeaders(1).First();
        }

        public bool DoubleEqual(double a, double b)
        {
            return Math.Abs(a - b) < 0.00001;
        }
    }

    /// <summary>Wrapper around <see cref="BlockPuller"/> that exposes private methods and properties using reflection.</summary>
    public class ExtendedBlockPuller : IBlockPuller, IDisposable
    {
        private readonly BlockPuller puller;

        public ExtendedBlockPuller(BlockPuller.OnBlockDownloadedCallback callback, IChainState chainState, ProtocolVersion protocolVersion, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.puller = new BlockPuller(callback, chainState, protocolVersion, dateTimeProvider, loggerFactory);
        }

        public Dictionary<int, IBlockPullerBehavior> PullerBehaviorsByPeerId => (Dictionary<int, IBlockPullerBehavior>)this.puller.GetMemberValue("pullerBehaviorsByPeerId");

        public Dictionary<uint256, AssignedDownload> AssignedDownloadsByHash => (Dictionary<uint256, AssignedDownload>)this.puller.GetMemberValue("assignedDownloadsByHash");

        public Queue<DownloadJob> ReassignedJobsQueue => (Queue<DownloadJob>)this.puller.GetMemberValue("reassignedJobsQueue");

        public Queue<DownloadJob> DownloadJobsQueue => (Queue<DownloadJob>)this.puller.GetMemberValue("downloadJobsQueue");

        public LinkedList<AssignedDownload> AssignedDownloadsSorted => (LinkedList<AssignedDownload>)this.puller.GetMemberValue("assignedDownloadsSorted");

        public AsyncManualResetEvent ProcessQueuesSignal => (AsyncManualResetEvent)this.puller.GetMemberValue("processQueuesSignal");

        public Dictionary<int, List<ChainedHeader>> AssignedHeadersByPeerId => (Dictionary<int, List<ChainedHeader>>)this.puller.GetMemberValue("assignedHeadersByPeerId");

        public int PeerSpeedLimitWhenNotInIbdBytesPerSec => typeof(BlockPuller).GetPrivateConstantValue<int>("PeerSpeedLimitWhenNotInIbdBytesPerSec");

        public int ImportantHeightMargin => typeof(BlockPuller).GetPrivateConstantValue<int>("ImportantHeightMargin");

        public int StallingLoopIntervalMs => typeof(BlockPuller).GetPrivateConstantValue<int>("StallingLoopIntervalMs");

        public int MaxSecondsToDeliverBlock => typeof(BlockPuller).GetPrivateConstantValue<int>("MaxSecondsToDeliverBlock");

        public void RecalculateQualityScoreLocked(IBlockPullerBehavior pullerBehavior, int peerId)
        {
            this.puller.InvokeMethod("RecalculateQualityScoreLocked", pullerBehavior, peerId);
        }

        public int GetTotalSpeedOfAllPeersBytesPerSec()
        {
            return (int)this.puller.InvokeMethod("GetTotalSpeedOfAllPeersBytesPerSec");
        }

        public Task AssignDownloadJobsAsync()
        {
            return (Task)this.puller.InvokeMethod("AssignDownloadJobsAsync");
        }

        public void SetMaxBlocksBeingDownloaded(int value)
        {
            this.puller.SetPrivateVariableValue("maxBlocksBeingDownloaded", value);
        }

        public void CheckStalling()
        {
            this.puller.InvokeMethod("CheckStalling");
        }

        public int GetMaxBlocksBeingDownloaded()
        {
            return (int)this.puller.GetMemberValue("maxBlocksBeingDownloaded");
        }

        public List<AssignedDownload> DistributeHeadersLocked(DownloadJob downloadJob, List<uint256> failedHashes, int emptySlots)
        {
            return (List<AssignedDownload>)this.puller.InvokeMethod("DistributeHeadersLocked", downloadJob, failedHashes, emptySlots);
        }

        public void Initialize() { this.puller.Initialize(); }

        public double GetAverageBlockSizeBytes() { return this.puller.GetAverageBlockSizeBytes(); }

        public void OnIbdStateChanged(bool isIbd) { this.puller.OnIbdStateChanged(isIbd); }

        public void NewPeerTipClaimed(INetworkPeer peer, ChainedHeader newTip) { this.puller.NewPeerTipClaimed(peer, newTip); }

        public void PeerDisconnected(int peerId) { this.puller.PeerDisconnected(peerId); }

        public void RequestBlocksDownload(List<ChainedHeader> headers) { this.puller.RequestBlocksDownload(headers); }

        public void PushBlock(uint256 blockHash, Block block, int peerId) { this.puller.PushBlock(blockHash, block, peerId); }

        public void Dispose() { this.puller.Dispose(); }
    }

    /// <summary>Wrapper around <see cref="NetworkPeerBehavior"/> that exposes private methods and properties using reflection.</summary>
    public class ExtendedBlockPullerBehavior : NetworkPeerBehavior, IBlockPullerBehavior
    {
        public readonly List<uint256> RequestedHashes;

        public bool ProvidedIbdState;

        public bool RecalculateQualityScoreWasCalled;

        public bool ShouldThrowAtRequestBlocksAsync;

        public double? OverrideQualityScore;

        private readonly BlockPullerBehavior underlyingBehavior;

        public ExtendedBlockPullerBehavior(IBlockPuller blockPuller, IInitialBlockDownloadState ibdState, ILoggerFactory loggerFactory)
        {
            this.ShouldThrowAtRequestBlocksAsync = false;
            this.RecalculateQualityScoreWasCalled = false;
            this.RequestedHashes = new List<uint256>();
            this.underlyingBehavior = new BlockPullerBehavior(blockPuller, ibdState, loggerFactory);
        }

        public void OnIbdStateChanged(bool isIbd)
        {
            this.underlyingBehavior.OnIbdStateChanged(isIbd);
            this.ProvidedIbdState = isIbd;
        }

        public Task RequestBlocksAsync(List<uint256> hashes)
        {
            this.RequestedHashes.AddRange(hashes);

            if (this.ShouldThrowAtRequestBlocksAsync)
                throw new OperationCanceledException();

            return Task.CompletedTask;
        }

        public double QualityScore
        {
            get
            {
                if (this.OverrideQualityScore != null)
                    return this.OverrideQualityScore.Value;

                return this.underlyingBehavior.QualityScore;
            }
        }  

        public int SpeedBytesPerSecond => this.underlyingBehavior.SpeedBytesPerSecond;

        public ChainedHeader Tip { get => this.underlyingBehavior.Tip; set => this.underlyingBehavior.Tip = value; }

        public void AddSample(long blockSizeBytes, double delaySeconds) { this.underlyingBehavior.AddSample(blockSizeBytes, delaySeconds); }

        public AverageCalculator AverageSizeBytes => this.underlyingBehavior.averageSizeBytes;

        public void Penalize(double delaySeconds, int notDeliveredBlocksCount) { this.underlyingBehavior.Penalize(delaySeconds, notDeliveredBlocksCount); }

        public void RecalculateQualityScore(int bestSpeedBytesPerSecond)
        {
            this.underlyingBehavior.RecalculateQualityScore(bestSpeedBytesPerSecond);
            this.RecalculateQualityScoreWasCalled = true;
        }

        public override object Clone() { return null; }

        protected override void AttachCore() { }

        protected override void DetachCore() { }
    }
}