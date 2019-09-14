using System;
using System.Linq;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.RPC.Exceptions;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.RPC;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Compatibility
{
    /// <summary>
    /// stratisX test fixture for RPC tests.
    /// </summary>
    public class RpcTestFixtureStratisX : RpcTestFixtureBase
    {
        /// <inheritdoc />
        protected override void InitializeFixture()
        {
            // TODO: Make a ReadyBlockChainData for these tests
            this.Builder = NodeBuilder.Create(this);
            this.Node = this.Builder.CreateStratisXNode().Start();

            // Add a peer node and connect the nodes together
            var config = new NodeConfigParameters() { { "addnode", this.Node.Endpoint.ToString() } };
            this.Builder.CreateStratisXNode(configParameters: config).Start();

            this.RpcClient = this.Node.CreateRPCClient();

            // Generate 11 blocks
            this.RpcClient.SendCommand(RPCOperations.generate, 11);

            var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;
            TestBase.WaitLoop(() => this.RpcClient.GetBlockCount() >= 11, cancellationToken: cancellationToken);
            //TestBase.WaitLoop(() => this.RpcClient.GetPeersInfo().Length > 0, cancellationToken: cancellationToken);

            // TODO: Disable staking
        }

        public override void Dispose()
        {
            this.Builder.Dispose();
        }
    }

    public class StratisXRPCTests : IClassFixture<RpcTestFixtureStratisX>
    {
        private readonly RpcTestFixtureStratisX rpcTestFixture;

        public StratisXRPCTests(RpcTestFixtureStratisX RpcTestFixture)
        {
            this.rpcTestFixture = RpcTestFixture;
        }

        [Fact]
        public void GetBlockCount()
        {
            RPCClient rpc = this.rpcTestFixture.RpcClient;
            int blockCount = rpc.GetBlockCount();

            Assert.Equal(11, blockCount);
        }

        [Fact]
        public void GetBalanceReturnsNonzero()
        {
            RPCClient rpc = this.rpcTestFixture.RpcClient;
            Money balance = rpc.GetBalance();

            // 1 block's worth of rewards are outside the maturity window and are spendable.
            Assert.Equal(Money.Coins(4), balance);
        }

        [Fact(Skip = "StratisX does not implement the gettxout RPC")]
        public void GetTxOutWithValidTxThenReturnsCorrectUnspentTx()
        {
            RPCClient rpc = this.rpcTestFixture.RpcClient;
            UnspentCoin[] unspent = rpc.ListUnspent();
            Assert.True(unspent.Any());
            UnspentCoin coin = unspent[0];
            UnspentTransaction resultTxOut = rpc.GetTxOut(coin.OutPoint.Hash, coin.OutPoint.N, true);
            Assert.Equal((int)coin.Confirmations, resultTxOut.confirmations);
            Assert.Equal(coin.Amount.ToDecimal(MoneyUnit.BTC), resultTxOut.value);
            Assert.Equal(coin.Address.ToString(), resultTxOut.scriptPubKey.addresses[0]);
        }

        [Fact(Skip = "StratisX does not implement the gettxout RPC")]
        public async void GetTxOutAsyncWithValidTxThenReturnsCorrectUnspentTxAsync()
        {
            RPCClient rpc = this.rpcTestFixture.RpcClient;
            UnspentCoin[] unspent = rpc.ListUnspent();
            Assert.True(unspent.Any());
            UnspentCoin coin = unspent[0];
            UnspentTransaction resultTxOut = await rpc.GetTxOutAsync(coin.OutPoint.Hash, coin.OutPoint.N, true);
            Assert.Equal((int)coin.Confirmations, resultTxOut.confirmations);
            Assert.Equal(coin.Amount.ToDecimal(MoneyUnit.BTC), resultTxOut.value);
            Assert.Equal(coin.Address.ToString(), resultTxOut.scriptPubKey.addresses[0]);
        }

        [Fact]
        public void GetBlockCountAsyncWithValidChainReturnsCorrectCount()
        {
            RPCClient rpc = this.rpcTestFixture.RpcClient;
            int blkCount = rpc.GetBlockCountAsync().Result;
            Assert.Equal(11, blkCount);
        }

        [Fact]
        public void GetPeersInfoWithValidPeersThenReturnsPeerInfo()
        {
            PeerInfo[] peers = this.rpcTestFixture.RpcClient.GetPeersInfo();
            Assert.NotEmpty(peers);
        }

        [Fact(Skip = "StratisX does not implement the estimatefeerate RPC")]
        public void EstimateFeeRateReturnsCorrectValues()
        {
            RPCClient rpc = this.rpcTestFixture.RpcClient;
            Assert.Throws<NoEstimationException>(() => rpc.EstimateFeeRate(1));
            Assert.Equal(Money.Coins(50m), rpc.GetBalance(1, false));
            Assert.Equal(Money.Coins(50m), rpc.GetBalance());
        }

        [Fact(Skip = "StratisX does not implement the fundrawtransaction RPC")]
        public void FundRawTransactionWithValidTxsThenReturnsCorrectResponse()
        {
            var k = new Key();
            var tx = new Transaction();
            tx.Outputs.Add(new TxOut(Money.Coins(1), k));
            RPCClient rpc = this.rpcTestFixture.RpcClient;
            FundRawTransactionResponse result = rpc.FundRawTransaction(tx);
            TestFundRawTransactionResult(tx, result);

            result = rpc.FundRawTransaction(tx, new FundRawTransactionOptions());
            TestFundRawTransactionResult(tx, result);
            FundRawTransactionResponse result1 = result;

            BitcoinAddress change = rpc.GetNewAddress();
            BitcoinAddress change2 = rpc.GetRawChangeAddress();
            result = rpc.FundRawTransaction(tx, new FundRawTransactionOptions()
            {
                FeeRate = new FeeRate(Money.Satoshis(50), 1),
                IncludeWatching = true,
                ChangeAddress = change,
            });
            TestFundRawTransactionResult(tx, result);
            Assert.True(result1.Fee < result.Fee);
            Assert.Contains(result.Transaction.Outputs, o => o.ScriptPubKey == change.ScriptPubKey);
        }

        private static void TestFundRawTransactionResult(Transaction tx, FundRawTransactionResponse result)
        {
            Assert.Equal(tx.Version, result.Transaction.Version);
            Assert.True(result.Transaction.Inputs.Count > 0);
            Assert.True(result.Transaction.Outputs.Count > 1);
            Assert.True(result.ChangePos != -1);
            Assert.Equal(Money.Coins(50m) - result.Transaction.Outputs.Select(txout => txout.Value).Sum(), result.Fee);
        }

        [Fact]
        public void InvalidCommandSendRPCException()
        {
            var ex = Assert.Throws<RPCException>(() => this.rpcTestFixture.RpcClient.SendCommand("donotexist"));
            Assert.True(ex.RPCCode == RPCErrorCode.RPC_METHOD_NOT_FOUND);
        }
    }
}