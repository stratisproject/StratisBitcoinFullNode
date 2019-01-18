using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using NBitcoin;
using Newtonsoft.Json;
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
                var coins = rpcClient.ListUnspent((int)network.Consensus.CoinbaseMaturity + 2, 99999, minerAddress);
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
                Action action = () => rpcClient.SendToAddress(aliceAddress, Money.Coins(1.0m));
                action.Should().Throw<RPCException>()
                    .Which.RPCCode.Should().Be(RPCErrorCode.RPC_WALLET_UNLOCK_NEEDED);

                // Unlock and lock case.
                rpcClient.WalletPassphrase("password", 60);
                rpcClient.SendCommand(RPCOperations.walletlock);
                action = () => rpcClient.SendToAddress(aliceAddress, Money.Coins(1.0m));
                action.Should().Throw<RPCException>()
                    .Which.RPCCode.Should().Be(RPCErrorCode.RPC_WALLET_UNLOCK_NEEDED);

                // Unlock timesout case.
                rpcClient.WalletPassphrase("password", 10);
                Thread.Sleep(30 * 1000); // 30 seconds.
                action = () => rpcClient.SendToAddress(aliceAddress, Money.Coins(1.0m));
                action.Should().Throw<RPCException>()
                    .Which.RPCCode.Should().Be(RPCErrorCode.RPC_WALLET_UNLOCK_NEEDED);
            }
        }

        [Fact]
        public void TestRpcSendManyWithInvalidParametersFails()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                Network network = new BitcoinRegTest();
                var node = builder.CreateStratisPowNode(network).WithWallet().Start();

                RPCClient rpcClient = node.CreateRPCClient();
                rpcClient.WalletPassphrase("password", 60);

                // Test with empty list of addresses
                var addresses = new Dictionary<string, decimal>();
                var addressesJson = JsonConvert.SerializeObject(addresses);
                Action action = () => rpcClient.SendCommand(RPCOperations.sendmany, string.Empty, addressesJson);
                action.Should().Throw<RPCException>().Which.RPCCode.Should().Be(RPCErrorCode.RPC_INVALID_PARAMETER);

                // Test with malformed Json.
                addressesJson = "[\"address\"";
                action = () => rpcClient.SendCommand(RPCOperations.sendmany, string.Empty, addressesJson);
                action.Should().Throw<RPCException>().Which.RPCCode.Should().Be(RPCErrorCode.RPC_PARSE_ERROR);
            }
        }
        
        [Fact]
        public void TestRpcSendManyWithValidDataIsSuccessful()
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
                var bob = new Key().GetBitcoinSecret(network);
                var bobAddress = bob.GetAddress();

                rpcClient.WalletPassphrase("password", 60);

                // Test with just defaults.
                const decimal coinsForAlice = 1.0m;
                const decimal coinsForBob = 2.0m;
                var addresses = new Dictionary<string, decimal>
                {
                    { aliceAddress.ToString(), coinsForAlice },
                    { bobAddress.ToString(), coinsForBob }
                };

                var addressesJson = JsonConvert.SerializeObject(addresses);
                var response = rpcClient.SendCommand(RPCOperations.sendmany, string.Empty, addressesJson);
                var txid = new uint256(response.ResultString);

                // Check the hash calculated correctly.
                var tx = rpcClient.GetRawTransaction(txid);
                txid.Should().Be(tx.GetHash());

                // Check the output is the right amount.
                var aliceCoins = tx.Outputs.AsCoins().First(c => c.ScriptPubKey == aliceAddress.ScriptPubKey);
                aliceCoins.Amount.Should().Be(Money.Coins(coinsForAlice));

                var bobCoins = tx.Outputs.AsCoins().First(c => c.ScriptPubKey == bobAddress.ScriptPubKey);
                bobCoins.Amount.Should().Be(Money.Coins(coinsForBob));

                // TODO: Enable test below once WalletTransactionHandler supports recepients with SubtractFeeFromAmount=true
                // Test option to subtract fees from outputs.
                //var subtractFeeAddresses = new[] { aliceAddress.ToString(), bobAddress.ToString() };
                //response = rpcClient.SendCommand(RPCOperations.sendmany, string.Empty, addressesJson, 0, string.Empty, subtractFeeAddresses);
                //txid = new uint256(response.ResultString);

                // Check the hash calculated correctly.
                //tx = rpcClient.GetRawTransaction(txid);
                //txid.Should().Be(tx.GetHash());

                // Check the output is the right amount.
                //aliceCoins = tx.Outputs.AsCoins().First(c => c.ScriptPubKey == aliceAddress.ScriptPubKey);
                //aliceCoins.Amount.Should().NotBeSameAs(Money.Coins(coinsForAlice));

                //bobCoins = tx.Outputs.AsCoins().First(c => c.ScriptPubKey == bobAddress.ScriptPubKey);
                //bobCoins.Amount.Should().NotBeSameAs(Money.Coins(coinsForBob));
            }
        }  
        
        [Fact(Skip = "This test takes a really long time to run because it tests a specific case. So only run explicitly.")]
        public void TestRpcSendToAddressSpendRefreshesTimeout()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                Network network = new BitcoinRegTest();
                var node = builder.CreateStratisPowNode(network).WithWallet().Start();

                RPCClient rpcClient = node.CreateRPCClient();
                int blocksMined = (int)network.Consensus.CoinbaseMaturity + 1;
                TestHelper.MineBlocks(node, blocksMined);
                TestHelper.WaitLoop(() => node.FullNode.GetBlockStoreTip().Height == blocksMined);

                var alice = new Key().GetBitcoinSecret(network);
                var aliceAddress = alice.GetAddress();

                // Unlock for 10 minutes.
                rpcClient.WalletPassphrase("password", 10 * 60);

                // Send a transaction.
                rpcClient.SendToAddress(aliceAddress, Money.Coins(1.0m)).Should().NotBeNull();

                // Mine that transaction.
                TestHelper.MineBlocks(node, 1);
                blocksMined++;
                TestHelper.WaitLoop(() => node.FullNode.GetBlockStoreTip().Height == blocksMined);

                // Wait 6 minutes and then send another transaction, should still be unlocked.
                Thread.Sleep(6 * 60 * 1000);

                var bob = new Key().GetBitcoinSecret(network);
                var bobAddress = bob.GetAddress();
                rpcClient.SendToAddress(bobAddress, Money.Coins(1.0m)).Should().NotBeNull();

                // Mine that transaction.
                TestHelper.MineBlocks(node, 1);
                blocksMined++;
                TestHelper.WaitLoop(() => node.FullNode.GetBlockStoreTip().Height == blocksMined);

                // Now wait 5 minutes so the wallet should be back locked and a transaction should fail.
                Thread.Sleep(5 * 60 * 1000);
                Action action = () => rpcClient.SendToAddress(aliceAddress, Money.Coins(1.0m));
                action.Should().Throw<RPCException>()
                    .Which.RPCCode.Should().Be(RPCErrorCode.RPC_WALLET_UNLOCK_NEEDED);
            }
        }

        [Fact]
        public void TestRpcSendToAddressInsufficientFunds()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                Network network = new BitcoinRegTest();
                var node = builder.CreateStratisPowNode(network).WithWallet().Start();

                RPCClient rpcClient = node.CreateRPCClient();
                int blocksMined = (int)network.Consensus.CoinbaseMaturity + 1;
                TestHelper.MineBlocks(node, blocksMined);
                TestHelper.WaitLoop(() => node.FullNode.GetBlockStoreTip().Height == blocksMined);

                var alice = new Key().GetBitcoinSecret(network);
                var aliceAddress = alice.GetAddress();

                rpcClient.WalletPassphrase("password", 30);
                Action action = () => rpcClient.SendToAddress(aliceAddress, Money.Coins(10000.0m));
                action.Should().Throw<RPCException>()
                    .Which.RPCCode.Should().Be(RPCErrorCode.RPC_WALLET_ERROR);
            }
        }
    }
}
