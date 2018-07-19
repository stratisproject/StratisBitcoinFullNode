﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
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
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header 10).
        /// Cached headers contain nothing. <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6.
        /// Make sure that <see cref="GetHeadersPayload"/> wasn't sent to the peer, <see cref="ConsensusManager.HeadersPresented"/> wasn't called. Return value is <c>null</c>.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_ConsensusTipAdvancedBuNoCachedHeadersAsync()
        {
            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[5], null, this.headers[10]);

            ConnectNewHeadersResult result = await behavior.ConsensusTipChangedAsync(this.headers[6]);

            Assert.Null(result);
            Assert.Equal(0, this.helper.GetHeadersPayloadSentTimes);
            Assert.Equal(0, this.helper.HeadersPresentedCalledTimes);
        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header 10). Cached headers have items 11 to 12.
        /// <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6.
        /// Make sure <see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> == 12, cached headers are empty and <see cref="GetHeadersPayload"/> was sent to the peer.
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

            ConnectNewHeadersResult result = await behavior.ConsensusTipChangedAsync(this.headers[6]);

            Assert.Equal(this.headers[12], behavior.ExpectedPeerTip);
            Assert.Empty(this.helper.GetCachedHeaders(behavior));
            Assert.Equal(1, this.helper.GetHeadersPayloadSentTimes);
            Assert.Equal(result.Consumed, this.headers[12]);
        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header 10).
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

            ConnectNewHeadersResult result = await behavior.ConsensusTipChangedAsync(this.headers[6]);

            Assert.Equal(this.headers[40], behavior.ExpectedPeerTip);
            Assert.Equal(0, this.helper.GetHeadersPayloadSentTimes);
            Assert.Equal(result.Consumed, this.headers[40]);

            List<BlockHeader> cacheAfterTipChanged = this.helper.GetCachedHeaders(behavior);

            Assert.Equal(10, cacheAfterTipChanged.Count);

            for (int i = 41; i <= 50; i++)
                Assert.Contains(this.headers[i].Header, cacheAfterTipChanged);
        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header 10). Cached headers have items 14 to 15.
        /// <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6. Make sure that cached headers contain no elements,
        /// <see cref="GetHeadersPayload"/> was sent to the peer is called and <see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is still 10.
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

            ConnectNewHeadersResult result = await behavior.ConsensusTipChangedAsync(this.headers[6]);

            Assert.Equal(this.headers[10], behavior.ExpectedPeerTip);
            Assert.Equal(1, this.helper.GetHeadersPayloadSentTimes);
            Assert.Null(result);
            Assert.Empty(this.helper.GetCachedHeaders(behavior));
        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header 10). Cached headers have items 11 to 12.
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

            ConnectNewHeadersResult result = await behavior.ConsensusTipChangedAsync(this.headers[6]);

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
    }
}
