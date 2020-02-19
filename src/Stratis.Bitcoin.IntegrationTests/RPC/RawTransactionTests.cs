using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.Networks;
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

        // TODO: Implement SignRawTransaction
    }
}
