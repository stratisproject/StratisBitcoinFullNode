using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Tests.Base
{
    public class ConsensusManagerBehaviorTests
    {
        private List<ChainedHeader> headers;

        private readonly ConsensusManagerBehaviorTestsHelper helper;

        public ConsensusManagerBehaviorTests()
        {
            this.helper = new ConsensusManagerBehaviorTestsHelper();

            this.headers = ChainedHeadersHelper.CreateConsecutiveHeaders(100, null, true);
        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.BestReceivedTip"/> is header 10).
        /// Cached headers contain nothing. <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6.
        /// Make sure that <see cref="GetHeadersPayload"/> wasn't sent to the peer, <see cref="ConsensusManager.HeadersPresented"/> wasn't called.
        /// Return value is <c>null</c>.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_ConsensusTipAdvancedBuNoCachedHeadersAsync()
        {
            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[5], null, this.headers[10]);

            ConnectNewHeadersResult result = await behavior.ConsensusTipChangedAsync();

            Assert.Null(result);
            Assert.Equal(0, this.helper.GetHeadersPayloadSentTimes);
            Assert.Equal(0, this.helper.HeadersPresentedCalledTimes);
        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.BestReceivedTip"/> is header 10). Cached headers have items 11 to 12.
        /// <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6.
        /// Make sure <see cref="ConsensusManagerBehavior.BestReceivedTip"/> == 12, cached headers are empty and <see cref="GetHeadersPayload"/> was sent to the peer.
        /// Make sure headers up to header 12 were consumed.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_CachedHeadersConsumedFullyAsync()
        {
            var cache = new List<BlockHeader>() {this.headers[11].Header, this.headers[12].Header};

            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[5], cache, this.headers[10], NetworkPeerState.HandShaked,
                (presentedHeaders, triggerDownload) =>
                {
                    Assert.Equal(this.headers[12].Header, presentedHeaders.Last());

                    return new ConnectNewHeadersResult() {Consumed = this.headers[12]};
                });

            ConnectNewHeadersResult result = await behavior.ConsensusTipChangedAsync();

            Assert.Equal(this.headers[12], behavior.BestReceivedTip);
            Assert.Equal(this.headers[12], behavior.BestSentHeader);
            Assert.Empty(this.helper.GetCachedHeaders(behavior));
            Assert.Equal(1, this.helper.GetHeadersPayloadSentTimes);
            Assert.Equal(result.Consumed, this.headers[12]);
        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.BestReceivedTip"/> is header 10).
        /// Cached headers have items 11 to 50. Setup <see cref="ConsensusManager.HeadersPresented"/> to stop consumption when block 40 is reached.
        /// <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6. Make sure ExpectedPeerTip == 40,
        /// cached headers contain 10 items (41 to 50) and <see cref="GetHeadersPayload"/> wasn't sent to the peer.
        /// Make sure in return value headers up to header 40 were consumed.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_CachedHeadersConsumedPartiallyAsync()
        {
            var cache = new List<BlockHeader>();
            for (int i = 11; i <= 50; i++)
                cache.Add(this.headers[i].Header);

            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[5], cache, this.headers[10], NetworkPeerState.HandShaked,
                (presentedHeaders, triggerDownload) =>
                {
                    Assert.Equal(this.headers[50].Header, presentedHeaders.Last());

                    return new ConnectNewHeadersResult() {Consumed = this.headers[40]};
                });

            ConnectNewHeadersResult result = await behavior.ConsensusTipChangedAsync();

            Assert.Equal(this.headers[40], behavior.BestReceivedTip);
            Assert.Equal(this.headers[40], behavior.BestSentHeader);
            Assert.Equal(0, this.helper.GetHeadersPayloadSentTimes);
            Assert.Equal(result.Consumed, this.headers[40]);

            List<BlockHeader> cacheAfterTipChanged = this.helper.GetCachedHeaders(behavior);

            Assert.Equal(10, cacheAfterTipChanged.Count);

            for (int i = 41; i <= 50; i++)
                Assert.Contains(this.headers[i].Header, cacheAfterTipChanged);
        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.BestReceivedTip"/> is header 10). Cached headers have items 14 to 15.
        /// <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6. Make sure that cached headers contain no elements,
        /// <see cref="GetHeadersPayload"/> was sent to the peer is called and <see cref="ConsensusManagerBehavior.BestReceivedTip"/> is still 10.
        /// Make sure return value is <c>null</c>.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_NotAbleToConnectCachedHeadersAsync()
        {
            var cache = new List<BlockHeader>() { this.headers[14].Header, this.headers[15].Header };

            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[5], cache, this.headers[10], NetworkPeerState.HandShaked,
                (presentedHeaders, triggerDownload) =>
                {
                    Assert.Equal(this.headers[14].Header, presentedHeaders.First());

                    throw new ConnectHeaderException();
                });

            ConnectNewHeadersResult result = await behavior.ConsensusTipChangedAsync();

            Assert.Equal(this.headers[10], behavior.BestReceivedTip);
            Assert.Equal(this.headers[10], behavior.BestSentHeader);
            Assert.Equal(1, this.helper.GetHeadersPayloadSentTimes);
            Assert.Null(result);
            Assert.Empty(this.helper.GetCachedHeaders(behavior));
        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.BestReceivedTip"/> is header 10). Cached headers have items 11 to 12.
        /// Peer is not attached (attached peer is <c>null</c>). <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6.
        /// Make sure return value is <c>null</c>.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_PeerNotAttachedAsync()
        {
            var cache = new List<BlockHeader>() { this.headers[11].Header, this.headers[12].Header };

            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[5], cache, this.headers[10]);

            // That will set peer to null.
            behavior.Dispose();

            ConnectNewHeadersResult result = await behavior.ConsensusTipChangedAsync();

            Assert.Equal(0, this.helper.GetHeadersPayloadSentTimes);
            Assert.Equal(0, this.helper.HeadersPresentedCalledTimes);
            Assert.Null(result);
        }

        /// <summary>
        /// Consensus tip is at header 10. We are in IBD. Node receives a message with <see cref="GetHeadersPayload"/>
        /// with <see cref="BlockLocator"/> generated from block 5. <see cref="HeadersPayload"/> wasn't sent.
        /// </summary>
        [Fact]
        public async Task ProcessGetHeadersAsync_DontAnswerIfInIBDAsync()
        {
            this.helper.IsIBD = true;
            this.helper.CreateAndAttachBehavior(this.headers[10]);

            await this.helper.ReceivePayloadAsync(this.helper.CreateGetHeadersPayload(this.headers[5]));

            Assert.Empty(this.helper.HeadersPayloadsSent);
        }

        /// <summary>
        /// Consensus tip is at header 10. We are in IBD. Node is whitelisted. Node receives a message with <see cref="GetHeadersPayload"/>
        /// with <see cref="BlockLocator"/> generated from block 5. <see cref="HeadersPayload"/> was sent with headers 6-10.
        /// </summary>
        [Fact]
        public async Task ProcessGetHeadersAsync_AnswerToWHitelistedPeersInIBDAsync()
        {
            this.helper.IsIBD = true;
            this.helper.IsPeerWhitelisted = true;
            this.helper.CreateAndAttachBehavior(this.headers[10]);

            await this.helper.ReceivePayloadAsync(this.helper.CreateGetHeadersPayload(this.headers[5]));

            Assert.Single(this.helper.HeadersPayloadsSent);

            List<BlockHeader> headersSent = this.helper.HeadersPayloadsSent.First().Headers;
            Assert.Equal(5, headersSent.Count);
            for (int i = 6; i <= 10; i++)
                Assert.Equal(this.headers[i].Header, headersSent[i - 6]);
        }

        /// <summary>
        /// Consensus tip is at header 10. We are not in IBD. Node receives a message with <see cref="GetHeadersPayload"/>
        /// with <see cref="BlockLocator"/> generated from block 5. <see cref="HeadersPayload"/> was sent with headers 6-10.
        /// </summary>
        [Fact]
        public async Task ProcessGetHeadersAsync_HeadersSentNormallyAsync()
        {
            this.helper.CreateAndAttachBehavior(this.headers[10]);

            await this.helper.ReceivePayloadAsync(this.helper.CreateGetHeadersPayload(this.headers[5]));

            Assert.Single(this.helper.HeadersPayloadsSent);

            List<BlockHeader> headersSent = this.helper.HeadersPayloadsSent.First().Headers;
            Assert.Equal(5, headersSent.Count);
            for (int i = 6; i <= 10; i++)
                Assert.Equal(this.headers[i].Header, headersSent[i - 6]);
        }

        /// <summary>
        /// Consensus tip is at header 10. We are not in IBD. Node receives a message with <see cref="GetHeadersPayload"/>
        /// with <see cref="BlockLocator"/> containing 5 bogus headers. <see cref="HeadersPayload"/> wasn't sent.
        /// </summary>
        [Fact]
        public async Task ProcessGetHeadersAsync_BlockLocatorWithBogusHeadersIgnoredAsync()
        {
            this.helper.CreateAndAttachBehavior(this.headers[10]);

            List<ChainedHeader> bogusHeaders = ChainedHeadersHelper.CreateConsecutiveHeaders(5);
            var payload = new GetHeadersPayload(new BlockLocator() { Blocks = bogusHeaders.Select(x => x.HashBlock).ToList()});

            await this.helper.ReceivePayloadAsync(payload);

            Assert.Empty(this.helper.HeadersPayloadsSent);
        }

        /// <summary>
        /// Consensus tip is at header 5000. We are not in IBD. Node receives a message with <see cref="GetHeadersPayload"/>
        /// <see cref="BlockLocator"/> generated from block 1000. <see cref="HeadersPayload"/> was sent with
        /// headers 1001 to 1001 + maximum amount of headers according to protocol restrictions.
        /// </summary>
        [Fact]
        public async Task ProcessGetHeadersAsync_SendsHeadersWithCountLimitedByProtocolAsync()
        {
            this.headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5000, null, true);

            this.helper.CreateAndAttachBehavior(this.headers[5000]);

            await this.helper.ReceivePayloadAsync(this.helper.CreateGetHeadersPayload(this.headers[1000]));

            Assert.Single(this.helper.HeadersPayloadsSent);

            List<BlockHeader> headersSent = this.helper.HeadersPayloadsSent.First().Headers;
            int maxHeaders = typeof(ConsensusManagerBehavior).GetPrivateConstantValue<int>("MaxItemsPerHeadersMessage");
            Assert.Equal(maxHeaders, headersSent.Count);

            for (int i = 1001; i < 1001 + maxHeaders; i++)
                Assert.Equal(this.headers[i].Header, headersSent[i - 1001]);
        }

        /// <summary>
        /// Consensus tip is at header 100a. We are not in IBD. Node receives a message with <see cref="GetHeadersPayload"/>
        /// <see cref="BlockLocator"/> that contains headers 90b, 60b, 50a, 30a, 10a. <see cref="HeadersPayload"/> was sent with headers 51a to 100a.
        /// </summary>
        [Fact]
        public async Task ProcessGetHeadersAsync_SendsHeadersIfLocatorIsPartiallyOnAForkAsync()
        {
            this.helper.CreateAndAttachBehavior(this.headers[100]);

            List<ChainedHeader> chainBSuffix = ChainedHeadersHelper.CreateConsecutiveHeaders(50, this.headers[55]);

            var payload = new GetHeadersPayload(new BlockLocator() { Blocks = new List<uint256>()
            {
                chainBSuffix.Single(x => x.Height == 90).HashBlock,
                chainBSuffix.Single(x => x.Height == 60).HashBlock,
                this.headers[50].HashBlock,
                this.headers[30].HashBlock,
                this.headers[10].HashBlock
            }});

            await this.helper.ReceivePayloadAsync(payload);

            Assert.Single(this.helper.HeadersPayloadsSent);

            List<BlockHeader> headersSent = this.helper.HeadersPayloadsSent.First().Headers;
            Assert.Equal(50, headersSent.Count);

            for (int i = 51; i < 100; i++)
                Assert.Equal(this.headers[i].Header, headersSent[i - 51]);
        }

        /// <summary>
        /// Consensus tip is at header 5000. We are not in IBD. Node receives a message with <see cref="GetHeadersPayload"/>
        /// <see cref="BlockLocator"/> generated from block 1000 with a <see cref="GetHeadersPayload.HashStop"/> equal to 1500.
        /// Make sure <see cref="HeadersPayload"/> was called with headers 1001 to 1500.
        /// </summary>
        [Fact]
        public async Task ProcessGetHeadersAsync_SendsHeadersUpTpHashStopAsync()
        {
            this.headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5000, null, true);

            this.helper.CreateAndAttachBehavior(this.headers[5000]);

            await this.helper.ReceivePayloadAsync(this.helper.CreateGetHeadersPayload(this.headers[1000], this.headers[1500].HashBlock));

            Assert.Single(this.helper.HeadersPayloadsSent);

            List<BlockHeader> headersSent = this.helper.HeadersPayloadsSent.First().Headers;
            Assert.Equal(500, headersSent.Count);

            for (int i = 1001; i < 1500; i++)
                Assert.Equal(this.headers[i].Header, headersSent[i - 1001]);
        }

        /// <summary>
        /// Present 0 headers, make sure <inheritdoc cref="IConsensusManager.HeadersPresented"/> wasn't called and
        /// <see cref="GetHeadersPayload"/> wasn't sent.
        /// </summary>
        [Fact]
        public async Task ProcessHeadersAsync_EmptyHeadersMessageReceivedAsync()
        {
            this.helper.CreateAndAttachBehavior(this.headers[5]);

            await this.helper.ReceivePayloadAsync(new HeadersPayload());

            Assert.Equal(0, this.helper.HeadersPresentedCalledTimes);
            Assert.Equal(0, this.helper.GetHeadersPayloadSentTimes);
        }

        /// <summary>Present non-consecutive headers. Make sure peer was banned.</summary>
        [Fact]
        public async Task ProcessHeadersAsync_NonConsecutiveHeadersPresentedAsync()
        {
            this.helper.CreateAndAttachBehavior(this.headers[5]);

            List<ChainedHeader> headersToPresent = ChainedHeadersHelper.CreateConsecutiveHeaders(5);
            headersToPresent.AddRange(ChainedHeadersHelper.CreateConsecutiveHeaders(10));

            await this.helper.ReceivePayloadAsync(new HeadersPayload(headersToPresent.Select(x => x.Header)));

            Assert.True(this.helper.PeerWasBanned);
        }

        /// <summary>
        /// Initialize cached headers so it's full. Present some headers.
        /// Make sure <inheritdoc cref="IConsensusManager.HeadersPresented"/> wasn't called and <see cref="GetHeadersPayload"/> wasn't sent.
        /// </summary>
        [Fact]
        public async Task ProcessHeadersAsync_DontSyncAfterCacheIsFullAsync()
        {
            int cacheSyncHeadersThreshold = typeof(ConsensusManagerBehavior).GetPrivateConstantValue<int>("CacheSyncHeadersThreshold");

            List<ChainedHeader> cachedHeaders = ChainedHeadersHelper.CreateConsecutiveHeaders(cacheSyncHeadersThreshold + 1);

            this.helper.CreateAndAttachBehavior(this.headers[5], cachedHeaders.Select(x => x.Header).ToList());

            List<ChainedHeader> headersToPresent = ChainedHeadersHelper.CreateConsecutiveHeaders(10, cachedHeaders.Last());
            await this.helper.ReceivePayloadAsync(new HeadersPayload(headersToPresent.Select(x => x.Header)));

            Assert.Equal(0, this.helper.HeadersPresentedCalledTimes);
            Assert.Equal(0, this.helper.GetHeadersPayloadSentTimes);
        }

        /// <summary>
        /// Initialize cached headers with 1 item. Present 10 headers. Make sure cached headers
        /// now have 11 items and <see cref="GetHeadersPayload"/> wasn't sent.
        /// </summary>
        [Fact]
        public async Task ProcessHeadersAsync_DontSyncAfterCacheIsPopulatedAsync()
        {
            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[5], new List<BlockHeader>() { this.headers[1].Header });

            await this.helper.ReceivePayloadAsync(new HeadersPayload(this.headers.Skip(2).Take(10).Select(x => x.Header)));

            List<BlockHeader> cached = this.helper.GetCachedHeaders(behavior);
            Assert.Equal(11, cached.Count);

            for (int i = 0; i < 11; i++)
                Assert.Equal(this.headers[i + 1].Header, cached[i]);

            Assert.Equal(0, this.helper.GetHeadersPayloadSentTimes);
        }

        /// <summary>
        /// Consensus tip is at 10. We are not in IBD. Present headers from 12 to 20. Make sure <see cref="GetHeadersPayload"/> was sent.
        /// </summary>
        [Fact]
        public async Task ProcessHeadersAsync_SyncWhenCacheIsEmptyAsync()
        {
            this.helper.CreateAndAttachBehavior(this.headers[10], null, null, NetworkPeerState.HandShaked,
                (presentedHeaders, triggerDownload) => { throw new ConnectHeaderException(); });

            await this.helper.ReceivePayloadAsync(new HeadersPayload(this.headers.Skip(13).Take(8).Select(x => x.Header)));

            Assert.Equal(1, this.helper.GetHeadersPayloadSentTimes);
        }

        /// <summary>
        /// Consensus tip is at 10a. We are not in IBD. Mock consensus manager to throw checkpoints exception. Present some headers.
        /// Make sure <see cref="GetHeadersPayload"/> wasn't sent and peer was banned.
        /// </summary>
        [Fact]
        public async Task ProcessHeadersAsync_PeerThatViolatesCheckpointIsBannedAsync()
        {
            this.helper.CreateAndAttachBehavior(this.headers[10], null, null, NetworkPeerState.HandShaked,
                (presentedHeaders, triggerDownload) => { throw new CheckpointMismatchException(); });

            await this.helper.ReceivePayloadAsync(new HeadersPayload(this.headers.Take(50).Select(x => x.Header)));

            Assert.Equal(0, this.helper.GetHeadersPayloadSentTimes);
            Assert.True(this.helper.PeerWasBanned);
        }

        /// <summary>
        /// Consensus tip is at 10. We are not in IBD. Present headers 11-15, where one header is invalid.
        /// Make sure <see cref="GetHeadersPayload"/> wasn't sent and peer was banned.
        /// </summary>
        [Fact]
        public async Task ProcessHeadersAsync_PeerThatSentInvalidHeaderIsBannedAsync()
        {
            this.helper.CreateAndAttachBehavior(this.headers[10], null, null, NetworkPeerState.HandShaked,
                (presentedHeaders, triggerDownload) => { throw new HeaderInvalidException(); });

            await this.helper.ReceivePayloadAsync(new HeadersPayload(this.headers.Skip(11).Take(5).Select(x => x.Header)));

            Assert.Equal(0, this.helper.GetHeadersPayloadSentTimes);
            Assert.True(this.helper.PeerWasBanned);
        }

        /// <summary>
        /// Consensus tip is at 10. We are not in IBD. Present headers 11-15, where one header is invalid.
        /// Make sure <see cref="GetHeadersPayload"/> wasn't sent and peer was banned.
        /// </summary>
        [Fact]
        public async Task ProcessHeadersAsync_PeerThatSentInvalidHeaderThatThrowFromRuleEngineIsBannedAsync()
        {
            this.helper.CreateAndAttachBehavior(this.headers[10], null, null, NetworkPeerState.HandShaked,
                (presentedHeaders, triggerDownload) => { throw new ConsensusRuleException(ConsensusErrors.BadVersion); });

            await this.helper.ReceivePayloadAsync(new HeadersPayload(this.headers.Skip(11).Take(5).Select(x => x.Header)));

            Assert.Equal(0, this.helper.GetHeadersPayloadSentTimes);
            Assert.True(this.helper.PeerWasBanned);
        }

        /// <summary>
        /// Consensus tip is at 10. We are not in IBD. Present headers 11-15. Make sure <see cref="ConsensusManagerBehavior.BestReceivedTip"/>
        /// is header 15 and <see cref="GetHeadersPayload"/> was sent.
        /// </summary>
        [Fact]
        public async Task ProcessHeadersAsync_ConsumeAllHeadersAndAskForMoreAsync()
        {
            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[10], null, null, NetworkPeerState.HandShaked,
                (presentedHeaders, triggerDownload) =>
                {
                    return new ConnectNewHeadersResult() { Consumed = this.headers.Single(x => x.HashBlock == presentedHeaders.Last().GetHash()) };
                });

            await this.helper.ReceivePayloadAsync(new HeadersPayload(this.headers.Skip(11).Take(5).Select(x => x.Header)));

            Assert.Equal(this.headers[15], behavior.BestReceivedTip);
            Assert.Equal(1, this.helper.GetHeadersPayloadSentTimes);
        }

        /// <summary>
        /// Consensus tip is at 10. We are not in IBD. Setup <see cref="IConsensusManager"/> in a way that it will consume headers up to header 40.
        /// Present headers 11-50.  Make sure that <see cref="ConsensusManagerBehavior.BestReceivedTip"/> is header 40 and
        /// <see cref="GetHeadersPayload"/> wasn't sent. Cached headers contain headers from 41 to 50.
        /// </summary>
        [Fact]
        public async Task ProcessHeadersAsync_DontSyncAfterSomeHeadersConsumedAndSomeCachedAsync()
        {
            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[10], null, null, NetworkPeerState.HandShaked,
                (presentedHeaders, triggerDownload) =>
                {
                    return new ConnectNewHeadersResult() { Consumed = this.headers[40] };
                });

            await this.helper.ReceivePayloadAsync(new HeadersPayload(this.headers.Skip(11).Take(40).Select(x => x.Header)));

            Assert.Equal(this.headers[40], behavior.BestReceivedTip);
            Assert.Equal(0, this.helper.GetHeadersPayloadSentTimes);

            List<BlockHeader> cached = this.helper.GetCachedHeaders(behavior);
            Assert.Equal(10, cached.Count);

            for (int i = 41; i <= 50; i++)
                Assert.Equal(this.headers[i].Header, cached[i - 41]);
        }

        /// <summary>We receive more headers than max allowed. Make sure peer was banned.</summary>
        [Fact]
        public async Task ProcessHeadersAsync_BanPeerThatViolatedMaxHeadersCountAsync()
        {
            this.helper.CreateAndAttachBehavior(this.headers[10], null, null, NetworkPeerState.HandShaked,
                (presentedHeaders, triggerDownload) => { throw new ConsensusException(""); });


            int maxHeaders = typeof(ConsensusManagerBehavior).GetPrivateConstantValue<int>("MaxItemsPerHeadersMessage");

            List<ChainedHeader> headersToPresent = ChainedHeadersHelper.CreateConsecutiveHeaders(maxHeaders + 500, null, true);

            await this.helper.ReceivePayloadAsync(new HeadersPayload(headersToPresent.Select(x => x.Header)));

            Assert.Equal(0, this.helper.GetHeadersPayloadSentTimes);
            Assert.True(this.helper.PeerWasBanned);
        }

        /// <summary>
        /// Simulate that peer state became <see cref="NetworkPeerState.HandShaked"/>.
        /// Make sure <see cref="GetHeadersPayload"/> was sent.
        /// </summary>
        [Fact]
        public async Task OnStateChangedAsync_SyncOnHandshakeAsync()
        {
            this.helper.CreateAndAttachBehavior(this.headers[10]);

            // Peer's state is handshaked which is the current state. Calling OnStateChanged with old state which is connected.
            await this.helper.StateChanged.ExecuteCallbacksAsync(this.helper.PeerMock.Object, NetworkPeerState.Connected);

            Assert.Equal(1, this.helper.GetHeadersPayloadSentTimes);
        }

        /// <summary>
        /// Initialize <see cref="ConsensusManagerBehavior.BestReceivedTip"/> to be header 5. Call <see cref="ConsensusManagerBehavior.ResetPeerTipInformationAndSyncAsync"/>.
        /// Make sure <see cref="ConsensusManagerBehavior.BestReceivedTip"/> became <c>null</c> and <see cref="GetHeadersPayload"/> was sent.
        /// </summary>
        [Fact]
        public async Task ResetPeerTipInformationAndSyncAsync_ResyncsAndResetsAsync()
        {
            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[5], null, this.headers[5]);

            await behavior.ResetPeerTipInformationAndSyncAsync();

            Assert.Null(behavior.BestReceivedTip);
            Assert.Null(behavior.BestSentHeader);
            Assert.Equal(1, this.helper.GetHeadersPayloadSentTimes);
        }

        /// <summary>
        /// <see cref="ConsensusManagerBehavior.BestReceivedTip"/> is <c>null</c>. Consensus tip is at header 100.
        /// Simulate that peer state is now <see cref="NetworkPeerState.HandShaked"/>.
        /// Make sure <see cref="GetHeadersPayload"/> was sent to the peer and it contains some blocks with hashes over header 50.
        /// </summary>
        [Fact]
        public async Task ResyncAsync_SyncsIfStateIsHanshakedAsync()
        {
            this.helper.CreateAndAttachBehavior(this.headers[100]);

            await this.helper.StateChanged.ExecuteCallbacksAsync(this.helper.PeerMock.Object, NetworkPeerState.Connected);

            Assert.Equal(1, this.helper.GetHeadersPayloadSentTimes);

            List<uint256> sentHashes = this.helper.GetHeadersPayloadsSent.First().BlockLocator.Blocks;
            List<uint256> hashesOver50 = this.headers.Skip(51).Select(x => x.HashBlock).ToList();

            bool contains = false;
            foreach (uint256 sentHash in sentHashes)
            {
                if (hashesOver50.Contains(sentHash))
                {
                    contains = true;
                    break;
                }
            }

            Assert.True(contains);
        }

        /// <summary>
        /// <see cref="ConsensusManagerBehavior.BestReceivedTip"/> is header 50. Consensus tip is at header 100.
        /// Simulate that peer state is now <see cref="NetworkPeerState.HandShaked"/>.
        /// Make sure <see cref="GetHeadersPayload"/> was sent to the peer and it contains blocks with hashes below header 50 and over header 30, but no blocks over 50.
        /// </summary>
        [Fact]
        public async Task ResyncAsync_SendsProperLocatorAsync()
        {
            this.helper.CreateAndAttachBehavior(this.headers[100], null, this.headers[50]);

            await this.helper.StateChanged.ExecuteCallbacksAsync(this.helper.PeerMock.Object, NetworkPeerState.Connected);

            Assert.Equal(1, this.helper.GetHeadersPayloadSentTimes);

            List<uint256> sentHashes = this.helper.GetHeadersPayloadsSent.First().BlockLocator.Blocks;
            List<uint256> hashesOver50 = this.headers.Skip(51).Select(x => x.HashBlock).ToList();

            bool containsHashesOver50 = false;
            foreach (uint256 sentHash in sentHashes)
            {
                if (hashesOver50.Contains(sentHash))
                {
                    containsHashesOver50 = true;
                    break;
                }
            }

            Assert.False(containsHashesOver50);

            // Make sure contains hashes over 30.
            List<uint256> hashesOver30 = this.headers.Skip(31).Select(x => x.HashBlock).ToList();

            bool containsHashesOver30 = false;
            foreach (uint256 sentHash in sentHashes)
            {
                if (hashesOver30.Contains(sentHash))
                {
                    containsHashesOver30 = true;
                    break;
                }
            }

            Assert.True(containsHashesOver30);
        }

        /// <summary>
        /// Initialize <see cref="ConsensusManagerBehavior.BestSentHeader"/> with something.
        /// Call <see cref="ConsensusManagerBehavior.UpdateBestSentHeader"/> with <c>null</c>.
        /// Make sure <see cref="ConsensusManagerBehavior.BestSentHeader"/> didn't change.
        /// </summary>
        [Fact]
        public void UpdateBestSentHeader_DoesntChangeIfArgumentIsNull()
        {
            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[20], null, this.headers[10]);

            behavior.UpdateBestSentHeader(null);

            Assert.Equal(this.headers[10], behavior.BestSentHeader);
        }

        /// <summary>
        /// Initialize <see cref="ConsensusManagerBehavior.BestSentHeader"/> with <c>null</c>.
        /// Call <see cref="ConsensusManagerBehavior.UpdateBestSentHeader"/> with any header.
        /// Make sure <see cref="ConsensusManagerBehavior.BestSentHeader"/> was set.
        /// </summary>
        [Fact]
        public void UpdateBestSentHeader_ChangesIfPreviousValueWasNull()
        {
            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[20]);

            behavior.UpdateBestSentHeader(this.headers[10]);

            Assert.Equal(this.headers[10], behavior.BestSentHeader);
        }

        /// <summary>
        /// Initialize <see cref="ConsensusManagerBehavior.BestSentHeader"/> with 10.
        /// Call <see cref="ConsensusManagerBehavior.UpdateBestSentHeader"/> with header 5.
        /// Make sure <see cref="ConsensusManagerBehavior.BestSentHeader"/> wasn't changed.
        /// </summary>
        [Fact]
        public void UpdateBestSentHeader_DoesntChangeIfCalledWithAncestor()
        {
            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[20], null, this.headers[10]);

            behavior.UpdateBestSentHeader(this.headers[5]);

            Assert.Equal(this.headers[10], behavior.BestSentHeader);
        }

        /// <summary>
        /// Initialize <see cref="ConsensusManagerBehavior.BestSentHeader"/> with 10.
        /// Call <see cref="ConsensusManagerBehavior.UpdateBestSentHeader"/> with header 15.
        /// Make sure <see cref="ConsensusManagerBehavior.BestSentHeader"/> was set to header 15.
        /// </summary>
        [Fact]
        public void UpdateBestSentHeader_IsSetIfChainProlonged()
        {
            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[20], null, this.headers[10]);

            behavior.UpdateBestSentHeader(this.headers[15]);

            Assert.Equal(this.headers[15], behavior.BestSentHeader);
        }

        /// <summary>
        /// Initialize <see cref="ConsensusManagerBehavior.BestSentHeader"/> with 10.
        /// Call <see cref="ConsensusManagerBehavior.UpdateBestSentHeader"/> with header 15b where b is the chain that forks at 8a.
        /// Make sure <see cref="ConsensusManagerBehavior.BestSentHeader"/> was set to be 15b.
        /// </summary>
        [Fact]
        public void UpdateBestSentHeader_ChangedIfHeaderIsOnFork()
        {
            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[20], null, this.headers[10]);

            ChainedHeader headerOnChainB = ChainedHeadersHelper.CreateConsecutiveHeaders(7, this.headers[8]).Last();

            behavior.UpdateBestSentHeader(headerOnChainB);

            Assert.Equal(headerOnChainB, behavior.BestSentHeader);
        }
    }
}
