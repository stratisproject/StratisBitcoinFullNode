using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Stratis.Bitcoin.Base;
using Xunit;

namespace Stratis.Bitcoin.Tests.Base
{
    public class ConsensusManagerBehaviorTests
    {
        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header10).
        /// Cached headers contain nothing. <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6.
        /// Make sure that ResyncAsync wasn't called on the peer, CM.HeadersPresented wasn't called. Return value is <c>null</c>.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_CTAdvancedBuNoCachedHeaders()
        {

        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header10). Cached headers have items 11 to 12.
        /// <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6.
        /// Make sure <see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> == 12, Cached headers are empty and ResyncAsync was called.
        /// Make sure return headers up to header 12 were consumed.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_CachedHeadersConsumedFully()
        {

        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header10).
        /// Cached headers have items 11 to 50.  Setup CM.HeadersPresented to stop consumption when block 40 is reached.
        /// <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6. Make sure ExpectedPeerTip == 40,
        /// cached headers contain 10 items (41 to 50) and ResyncAsync wasn't called.
        /// Make sure return headers up to header 40 were consumed.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_CachedHeadersConsumedPartially()
        {

        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header10). Cached headers have items 14 to 15.
        /// <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6. Make sure that cached headers contain no elements,
        /// ResyncAsync is called and <see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is still 10. Make sure return value is <c>null</c>.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_NotAbleToConnectCachedHeaders()
        {

        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header10). Cached headers have items 11 to 12.
        /// Peer is not attached (AttachedPeer == null). <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6.
        /// Make sure return value is <c>null</c>.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_()
        {

        }
    }
}
