using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Features.FederatedPeg.IntegrationTests.Utils;
using Stratis.Features.FederatedPeg.Interfaces;
using Xunit;

namespace Stratis.Features.FederatedPeg.IntegrationTests
{
    public class LeaderTest
    {
        /// <summary>
        /// https://stratisplatform.sharepoint.com/:x:/g/EehmhCsUSRFKnUgJ1nZNDxoBlyxcGcmfwmCdgg7MJqkYgA?e=0iChWb
        /// ST-1_Standard_txt_in_sidechain
        /// </summary>
        //[Fact]
        [Fact(Skip = "Check out the  block is being notified as this will cause the leader to changee. At the moment in this test the leader doesn't change.")]
        public void LeaderChange()
        {
            using (SidechainTestContext context = new SidechainTestContext())
            {
                context.StartAndConnectNodes();

                IFederationGatewaySettings federationGatewaySettings = new FederationGatewaySettings(context.FedSide1.FullNode.Settings);
                ILeaderProvider leaderProvider = new LeaderProvider(federationGatewaySettings);

                PubKey currentLeader = leaderProvider.CurrentLeaderKey;

                int tipBefore = context.FedSide1.GetTip().Height;

                // TODO check blocks get mined and make sure the block notification will change
                // the leader.
                TestHelper.WaitLoop(
                    () =>
                    {
                        return context.FedSide1.GetTip().Height >= tipBefore + 5;
                    }
                );

                leaderProvider.CurrentLeaderKey.Should().NotBe(currentLeader);
            }
        }
    }
}
