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
        public void TestRpcGetBalanceIsSuccessful()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateStratisPowNode(KnownNetworks.RegTest).NotInIBD().WithWallet();
                builder.StartAll();
                RPCClient rpcClient = node.CreateRPCClient();

                TestHelper.MineBlocks(node, 2);
                TestHelper.WaitLoop(() => node.FullNode.GetBlockStoreTip().Height == 2);

                Money balance = rpcClient.GetBalance();
                Assert.Equal(Money.Coins(100), balance);
            }
        }

        [Fact]
        public void TestRpcGetTransactionIsSuccessful()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateStratisPowNode(KnownNetworks.RegTest).NotInIBD().WithWallet();
                builder.StartAll();
                RPCClient rpc = node.CreateRPCClient();             
                uint256 blockHash = rpc.Generate(1)[0];
                Block block = rpc.GetBlock(blockHash);
                RPCResponse walletTx = rpc.SendCommand(RPCOperations.gettransaction, block.Transactions[0].GetHash().ToString());
                walletTx.ThrowIfError();
            }
        }

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
