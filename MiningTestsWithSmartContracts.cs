using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.FederatedPeg.IntegrationTests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.IntegrationTests
{
    public class MiningTestsWithSmartContracts : TestBase
    {
        [Fact]
        public void Premine_Received_To_Federation()
        {
            using (SidechainNodeBuilder builder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            {
                // Set up builder and node
                builder.ConfigParameters.Add("sidechain", "true");
                builder.ConfigParameters.Add("federationips", "127.0.0.1");
                builder.ConfigParameters.Add("redeemscript", this.scriptAndAddresses.payToMultiSig.ToString());
                builder.ConfigParameters.Add("publickey", this.pubKeysByMnemonic[this.mnemonics[0]].ToString());
                CoreNode node = builder.CreateSidechainNodeWithSmartContracts(this.sidechainNetwork, this.sidechainNetwork.FederationKeys[0]);

                node.Start();

                // Wait for node to reach premine height 
                TestHelper.WaitLoop(() => node.FullNode.Chain.Height == node.FullNode.Network.Consensus.PremineHeight);

                // Ensure that coinbase contains premine reward and it goes to the fed.
                Block block = node.FullNode.Chain.Tip.Block;
                Transaction coinbase = block.Transactions[0];
                Assert.Single(coinbase.Outputs);
                Assert.Equal(node.FullNode.Network.Consensus.PremineReward, coinbase.Outputs[0].Value);
                Assert.Equal(this.scriptAndAddresses.payToMultiSig.PaymentScript, coinbase.Outputs[0].ScriptPubKey);
            }
        }


        [Fact]
        public void Nodes_Can_Connect()
        {
            using (SidechainNodeBuilder builder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            {
                // Set up builder and nodes
                builder.ConfigParameters.Add("sidechain", "true");
                builder.ConfigParameters.Add("federationips", "127.0.0.1");
                builder.ConfigParameters.Add("redeemscript", this.scriptAndAddresses.payToMultiSig.ToString());
                builder.ConfigParameters.Add("publickey", this.pubKeysByMnemonic[this.mnemonics[0]].ToString());
                CoreNode node1 = builder.CreateSidechainNodeWithSmartContracts(this.sidechainNetwork, this.sidechainNetwork.FederationKeys[0]);
                builder.ConfigParameters["publickey"] = this.pubKeysByMnemonic[this.mnemonics[1]].ToString();
                CoreNode node2 = builder.CreateSidechainNodeWithSmartContracts(this.sidechainNetwork, this.sidechainNetwork.FederationKeys[1]);

                // Start both nodes
                node1.Start();
                node2.Start();

                // Connect nodes. Will fail with timeout if not.
                TestHelper.Connect(node1, node2);
            }
        }

    }
}
