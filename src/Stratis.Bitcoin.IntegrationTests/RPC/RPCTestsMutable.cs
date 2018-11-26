using System.Linq;
using System.Threading;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
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
                var network = new BitcoinRegTest();
                CoreNode node = builder.CreateStratisPowNode(network).WithWallet().Start();

                RPCClient rpcClient = node.CreateRPCClient();

                int maturity = (int)network.Consensus.CoinbaseMaturity;

                TestHelper.MineBlocks(node, maturity);
                Assert.Equal(Money.Zero, rpcClient.GetBalance()); // test with defaults.

                TestHelper.MineBlocks(node, 1);
                Assert.Equal(Money.Coins(50), rpcClient.GetBalance(0, false)); // test with parameters.
            }
        }

        [Fact]
        public void TestRpcGetTransactionIsSuccessful()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateStratisPowNode(new BitcoinRegTest()).WithWallet().Start();

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
                var node = builder.CreateStratisPowNode(new BitcoinRegTest()).WithWallet().Start();

                RPCClient rpcClient = node.CreateRPCClient();

                TestHelper.MineBlocks(node, 2);
                TestHelper.WaitLoop(() => node.FullNode.GetBlockStoreTip().Height == 2);

                uint256 blockId = rpcClient.GetBestBlockHash();
                Block block = rpcClient.GetBlock(blockId);
                Assert.True(block.CheckMerkleRoot());
            }
        }

        [Fact]
        public void TestRpcListUnspentWithDefaultsIsSuccessful()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                Network network = new BitcoinRegTest();
                var node = builder.CreateStratisPowNode(network).WithWallet().Start();

                RPCClient rpcClient = node.CreateRPCClient();
                const int numCoins = 5;
                int blocksToMine = (int)network.Consensus.CoinbaseMaturity + numCoins;
                TestHelper.MineBlocks(node, blocksToMine);
                TestHelper.WaitLoop(() => node.FullNode.GetBlockStoreTip().Height == blocksToMine);

                var coins = rpcClient.ListUnspent();
                coins.Should().NotBeNull();
                coins.Length.Should().Be(numCoins);
            }
        }

        [Fact]
        public void TestRpcListUnspentWithParametersIsSuccessful()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                Network network = new BitcoinRegTest();
                var node = builder.CreateStratisPowNode(network).WithWallet().Start();

                RPCClient rpcClient = node.CreateRPCClient();
                const int numCoins = 5;
                int blocksToMine = (int)network.Consensus.CoinbaseMaturity + numCoins;
                TestHelper.MineBlocks(node, blocksToMine);
                TestHelper.WaitLoop(() => node.FullNode.GetBlockStoreTip().Height == blocksToMine);

                var minerAddress = BitcoinAddress.Create(node.MinerHDAddress.Address, network);

                // validate existing address and minconf
                var coins = rpcClient.ListUnspent((int)network.Consensus.CoinbaseMaturity+2, 99999, minerAddress);
                coins.Should().NotBeNull();
                coins.Length.Should().Be(numCoins - 1);

                // validate unknown address
                var unknownAddress = new Key().GetBitcoinSecret(network).GetAddress();                
                coins = rpcClient.ListUnspent(1, 99999, unknownAddress);
                coins.Should().NotBeNull();
                coins.Should().BeEmpty();

                // test just min conf
                var response = rpcClient.SendCommand(RPCOperations.listunspent, (int)network.Consensus.CoinbaseMaturity + 2);
                var result = response.ResultString;
                result.Should().NotBeNullOrEmpty();
            }
        }

        [Fact]
        public void TestRpcSendToAddressIsSuccessful()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                Network network = new BitcoinRegTest();
                var node = builder.CreateStratisPowNode(network).WithWallet().Start();

                RPCClient rpcClient = node.CreateRPCClient();
                int blocksToMine = (int)network.Consensus.CoinbaseMaturity + 1;
                TestHelper.MineBlocks(node, blocksToMine);
                TestHelper.WaitLoop(() => node.FullNode.GetBlockStoreTip().Height == blocksToMine);

                var alice = new Key().GetBitcoinSecret(network);
                var aliceAddress = alice.GetAddress();
                rpcClient.WalletPassphrase("password", 60);
                var txid = rpcClient.SendToAddress(aliceAddress, Money.Coins(1.0m));
                rpcClient.SendCommand(RPCOperations.walletlock);

                // Check the hash calculated correctly.
                var tx = rpcClient.GetRawTransaction(txid);
                Assert.Equal(txid, tx.GetHash());

                // Check the output is the right amount.
                var coin = tx.Outputs.AsCoins().First(c => c.ScriptPubKey == aliceAddress.ScriptPubKey);
                Assert.Equal(coin.Amount, Money.Coins(1.0m));
            }
        }

        [Fact]
        public void TestRpcSendToAddressCantSpendWhenLocked()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                Network network = new BitcoinRegTest();
                var node = builder.CreateStratisPowNode(network).WithWallet().Start();

                RPCClient rpcClient = node.CreateRPCClient();
                int blocksToMine = (int)network.Consensus.CoinbaseMaturity + 1;
                TestHelper.MineBlocks(node, blocksToMine);
                TestHelper.WaitLoop(() => node.FullNode.GetBlockStoreTip().Height == blocksToMine);

                var alice = new Key().GetBitcoinSecret(network);
                var aliceAddress = alice.GetAddress();

                // Not unlocked case.
                Assert.Throws<RPCException>(() => rpcClient.SendToAddress(aliceAddress, Money.Coins(1.0m)));

                // Unlock and lock case.
                rpcClient.WalletPassphrase("password", 60);
                rpcClient.SendCommand(RPCOperations.walletlock);
                Assert.Throws<RPCException>(() => rpcClient.SendToAddress(aliceAddress, Money.Coins(1.0m)));

                // Unlock timesout case.
                rpcClient.WalletPassphrase("password", 5);
                Thread.Sleep(120 * 1000); // 2 minutes.
                Assert.Throws<RPCException>(() => rpcClient.SendToAddress(aliceAddress, Money.Coins(1.0m)));
            }
        }
    }
}
