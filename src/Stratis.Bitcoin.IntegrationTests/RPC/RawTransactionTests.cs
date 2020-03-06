using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    public class RawTransactionTests
    {
        private readonly Network network;

        public RawTransactionTests()
        {
            this.network = new StratisRegTest();
        }

        [Fact]
        public void CanFundRawTransaction()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();

                var tx = this.network.CreateTransaction();
                var dest = new Key().ScriptPubKey;
                tx.Outputs.Add(new TxOut(Money.Coins(1.0m), dest));
                FundRawTransactionResponse funded = node.CreateRPCClient().FundRawTransaction(tx);

                Assert.NotNull(funded.Transaction);
                Assert.NotEmpty(funded.Transaction.Inputs);
            }
        }

        [Fact]
        public void CanSignRawTransaction()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();

                var tx = this.network.CreateTransaction();
                tx.Outputs.Add(new TxOut(Money.Coins(1.0m), new Key()));
                FundRawTransactionResponse funded = node.CreateRPCClient().FundRawTransaction(tx);

                // TODO: When using readydata the wallet password does not get populated on the CoreNode instance
                node.CreateRPCClient().WalletPassphrase("password", 600);

                Transaction signed = node.CreateRPCClient().SignRawTransaction(funded.Transaction);
                
                Assert.NotNull(signed);
                Assert.NotEmpty(signed.Inputs);

                foreach (var input in signed.Inputs)
                {
                    Assert.NotNull(input.ScriptSig);

                    // TODO: Would fail for a pure segwit transaction
                    Assert.NotEqual(input.ScriptSig, Script.Empty);
                }

                node.CreateRPCClient().SendRawTransaction(signed);

                TestBase.WaitLoop(() => node.CreateRPCClient().GetRawMempool().Length == 1);
            }
        }
    }
}
