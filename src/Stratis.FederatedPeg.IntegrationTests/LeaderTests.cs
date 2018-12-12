using System.Linq;
using FluentAssertions;

using NBitcoin;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.IntegrationTests.Utils;

using Xunit;

namespace Stratis.FederatedPeg.IntegrationTests
{
    public class LeaderTests : TestBase
    {
        /// <summary>
        /// https://stratisplatform.sharepoint.com/:x:/g/EehmhCsUSRFKnUgJ1nZNDxoBlyxcGcmfwmCdgg7MJqkYgA?e=0iChWb
        /// ST-1_Standard_txt_in_sidechain
        /// </summary>
        //[Fact]
        [Fact(Skip = "Check out the  block is being notified as this will cause the leader to changee. At the moment in this test the leader doesn't change.")]
        public void LeaderChange()
        {
            this.StartAndConnectNodes();

            IFederationGatewaySettings federationGatewaySettings = new FederationGatewaySettings(this.MainAndSideChainNodeMap["fedSide1"].Node.FullNode.Settings);
            ILeaderProvider leaderProvider = new LeaderProvider(federationGatewaySettings);

            PubKey currentLeader = leaderProvider.CurrentLeader;

            var tipBefore = this.MainAndSideChainNodeMap["fedSide1"].Node.GetTip().Height;

            // TODO check blocks get mined and make sure the block notification will change
            // the leader.
            TestHelper.WaitLoop(
                () =>
                {
                    return this.MainAndSideChainNodeMap["fedSide1"].Node.GetTip().Height >= tipBefore + 5;
                }
            );

            leaderProvider.CurrentLeader.Should().NotBe(currentLeader);
        }
    }
}
