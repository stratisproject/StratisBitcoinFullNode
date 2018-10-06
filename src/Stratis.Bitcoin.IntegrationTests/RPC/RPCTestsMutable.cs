using NBitcoin;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    /// <summary>
    /// RPC Integration tests that require their own node(s) for each test because they change node state.
    /// </summary>
    public class RPCTestsMutable
    {
        [Fact]
        public void TestRpcGetBlockWithValidHashIsSuccessful()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var node = builder.CreateStratisPowNode(KnownNetworks.RegTest).NotInIBD().WithWallet();
                builder.StartAll();
                RPCClient rpcClient = node.CreateRPCClient();

                TestHelper.MineBlocks(node, 2);
                TestHelper.WaitLoop(() => node.FullNode.GetBlockStoreTip().Height == 2);

                uint256 blockId = rpcClient.GetBestBlockHash();
                Block block = rpcClient.GetBlock(blockId);
                Assert.True(block.CheckMerkleRoot());
            }
        }
    }
}
