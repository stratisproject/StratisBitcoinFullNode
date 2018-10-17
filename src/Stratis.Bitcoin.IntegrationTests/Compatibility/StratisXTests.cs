using System;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Compatibility
{
    public class StratisXTests
    {
        [Fact]
        public void SBFNMinesBlocksXSyncs()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisXNode = builder.CreateStratisXNode(version: "2.0.0.5").Start();
                CoreNode stratisNode = builder.CreateStratisPosNode(new StratisRegTest()).NotInIBD().WithWallet().Start();

                RPCClient stratisXRpc = stratisXNode.CreateRPCClient();
                RPCClient stratisNodeRpc = stratisNode.CreateRPCClient();

                // Need to troubleshoot why TestHelper.Connect() does not work here, possibly unsupported RPC method.
                stratisXRpc.AddNode(stratisNode.Endpoint, false);
                stratisNodeRpc.AddNode(stratisXNode.Endpoint, false);

                // Similarly, the 'generate' RPC call is problematic on X. Possibly returning an unexpected JSON format.
                TestHelper.MineBlocks(stratisNode, 10);

                var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;
                TestHelper.WaitLoop(() => stratisNode.CreateRPCClient().GetBestBlockHash() == stratisXNode.CreateRPCClient().GetBestBlockHash(), cancellationToken: cancellationToken);
            }
        }
    }
}