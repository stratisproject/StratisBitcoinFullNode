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
        [Fact(Skip = "TODO: Check blocks get mined and make sure the block notification will alter the fed leader.")]
        public void LeaderChange()
        {
            using (SidechainNodeBuilder builder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            {
                builder.ConfigParameters.Add("sidechain", "true");
                builder.ConfigParameters.Add("redeemscript", this.scriptAndAddresses.payToMultiSig.ToString());
                builder.ConfigParameters.Add("publickey", this.pubKeysByMnemonic[this.mnemonics[0]].ToString());

                CoreNode node = builder.CreateSidechainNode(this.sidechainNetwork, this.sidechainNetwork.FederationKeys[0]);
                node.Start();
                node.EnableFastMining();

                IFederationGatewaySettings federationGatewaySettings = new FederationGatewaySettings(node.FullNode.Settings);
                ILeaderProvider leaderProvider = new LeaderProvider(federationGatewaySettings);

                PubKey currentLeader = leaderProvider.CurrentLeader;

                var tipBefore = node.GetTip().Height;

                // TODO check blocks get mined and make sure the block notification will change
                // the leader
                //TestHelper.WaitLoop(
                //    () =>
                //    {
                //        return node.GetTip().Height >= tipBefore + 5;
                //    }
                //    );

                //leaderProvider.CurrentLeader.Should().NotBe(currentLeader);
            }
        }
    }
}
