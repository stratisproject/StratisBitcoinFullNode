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
        private readonly List<ChainedHeader> headers;

        private readonly ConsensusManagerBehaviorTestsHelper helper;

        public ConsensusManagerBehaviorTests()
        {
            this.helper = new ConsensusManagerBehaviorTestsHelper();

            this.headers = ChainedHeadersHelper.CreateConsecutiveHeaders(100);

            this.headers.Insert(0, ChainedHeadersHelper.CreateGenesisChainedHeader());
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
        /// Make sure <see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> == 12, Cached headers are empty and <see cref="GetHeadersPayload"/> was sent to the peer.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_CachedHeadersConsumedFullyAsync()
        {
            var cache = new List<BlockHeader>() {this.headers[11].Header, this.headers[12].Header};

            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[5], cache, this.headers[10], NetworkPeerState.HandShaked,
                (presentedHeaders, triggerDownload) =>
                {
                    if (presentedHeaders.Last() == this.headers[12].Header)
                    {
                        return new ConnectNewHeadersResult() {Consumed = this.headers[12] };
                    }

                    return null;
                });

            ConnectNewHeadersResult result = await behavior.ConsensusTipChangedAsync(this.headers[6]);

            Assert.Equal(this.headers[12], behavior.ExpectedPeerTip);
            Assert.Empty(this.helper.GetCachedHeaders(behavior));
            Assert.Equal(1, this.helper.GetHeadersPayloadSentTimes);
            Assert.Equal(result.Consumed, this.headers[12]);
        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header 10).
        /// Cached headers have items 11 to 50.  Setup  <see cref="ConsensusManager.HeadersPresented"/> to stop consumption when block 40 is reached.
        /// <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6. Make sure ExpectedPeerTip == 40,
        /// cached headers contain 10 items (41 to 50) and <see cref="GetHeadersPayload"/> wasn't sent to the peer.
        /// Make sure in return value headers up to header 40 were consumed.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_CachedHeadersConsumedPartiallyAsync()
        {
            var cache = new List<BlockHeader>();
            for (int i= 11; i <= 50; i++)
                cache.Add(this.headers[i].Header);

            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[5], cache, this.headers[10], NetworkPeerState.HandShaked,
                (presentedHeaders, triggerDownload) =>
                {
                    if (presentedHeaders.Last() == this.headers[50].Header)
                    {
                        return new ConnectNewHeadersResult() { Consumed = this.headers[40] };
                    }

                    return null;
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
        /// <see cref="GetHeadersPayload"/> was sent to the peer is called and <see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is still 10. Make sure return value is <c>null</c>.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_NotAbleToConnectCachedHeadersAsync()
        {
            var cache = new List<BlockHeader>() { this.headers[14].Header, this.headers[15].Header };

            ConsensusManagerBehavior behavior = this.helper.CreateAndAttachBehavior(this.headers[5], cache, this.headers[10], NetworkPeerState.HandShaked,
                (presentedHeaders, triggerDownload) =>
                {
                    if (presentedHeaders.First() == this.headers[14].Header)
                    {
                        throw new ConnectHeaderException();
                    }

                    return null;
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
    }
}
