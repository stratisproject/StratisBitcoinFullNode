using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.RPC.Exceptions;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    /// <summary>
    /// Bitcoin test fixture for RPC tests.
    /// </summary>
    public class RpcTestFixtureBitcoin : RpcTestFixtureBase
    {
        /// <inheritdoc />
        protected override void InitializeFixture()
        {
            this.Builder = NodeBuilder.Create(this);
            this.Node = this.Builder.CreateBitcoinCoreNode().Start();
            this.InitializeTestWallet(this.Node.DataFolder);

            this.RpcClient = this.Node.CreateRPCClient();

            this.NetworkPeerClient = this.Node.CreateNetworkPeerClient();
            this.NetworkPeerClient.VersionHandshakeAsync().GetAwaiter().GetResult();

            // generate 101 blocks
            this.Node.GenerateAsync(101).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// These tests share a test fixture that creates a 101 block chain of transactions to be queried via RPC.
    /// This transaction chain should not be modified by the tests as the tests here assume the state of the
    /// chain is immutable.
    /// Tests that require modifying the transactions should be done in <see cref="RpcBitcoinMutableTests"/>
    /// and set up the chain in each test.
    /// </summary>
    public class RpcBitcoinImmutableTests : IClassFixture<RpcTestFixtureBitcoin>
    {
        private readonly RpcTestFixtureBitcoin rpcTestFixture;

        public RpcBitcoinImmutableTests(RpcTestFixtureBitcoin RpcTestFixture)
        {
            this.rpcTestFixture = RpcTestFixture;
        }

        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test CanGetTxOutFromRPC</seealso>
        /// </summary>
        [Fact]
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

        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test CanGetTxOutAsyncFromRPC</seealso>
        /// </summary>
        [Fact]
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

        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test CanUseAsyncRPC</seealso>
        /// </summary>
        [Fact]
        public void GetBlockCountAsyncWithValidChainReturnsCorrectCount()
        {
            RPCClient rpc = this.rpcTestFixture.RpcClient;
            int blkCount = rpc.GetBlockCountAsync().Result;
            Assert.Equal(101, blkCount);
        }

        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test CanGetPeersInfo</seealso>
        /// </summary>
        [Fact]
        public void GetPeersInfoWithValidPeersThenReturnsPeerInfo()
        {
            PeerInfo[] peers = this.rpcTestFixture.RpcClient.GetPeersInfo();
            Assert.NotEmpty(peers);
        }

        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test EstimateFeeRate</seealso>
        /// </summary>
        [Fact]
        public void EstimateFeeRateReturnsCorrectValues()
        {
            RPCClient rpc = this.rpcTestFixture.RpcClient;
            Assert.Throws<NoEstimationException>(() => rpc.EstimateFeeRate(1));
            Assert.Equal(Money.Coins(50m), rpc.GetBalance(1, false));
            Assert.Equal(Money.Coins(50m), rpc.GetBalance());
        }

        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test TestFundRawTransaction</seealso>
        /// </summary>
        [Fact]
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
