using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class PowMiningTests
    {
        [Fact]
        public void MinerCreateBlockWithTxSyncsToOtherNodeBlocksDetailsTheSame()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode miner = builder.CreateStratisPowNode();
                CoreNode receiver = builder.CreateStratisPowNode();

                builder.StartAll();

                miner.NotInIBD();
                receiver.NotInIBD();

                miner.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                HdAddress addr = miner.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                Features.Wallet.Wallet wallet = miner.FullNode.WalletManager().GetWalletByName("mywallet");
                Key key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;
                miner.SetDummyMinerSecret(new BitcoinSecret(key, miner.FullNode.Network));

                var blockHash = miner.GenerateStratisWithMiner(1);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(miner));

                var block = miner.CreateRPCClient().GetBlockHash(1);
                //TestHelper.WaitLoop(() => miner.CreateRPCClient().GetBlock(1));


                miner.CreateRPCClient().AddNode(receiver.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(miner, receiver));

                var getBlock = receiver.CreateRPCClient().GetBlockHash(1);

                Assert.NotNull(block);
                Assert.NotNull(getBlock);
                Assert.Equal(block, getBlock);
            }
        }
    }
}