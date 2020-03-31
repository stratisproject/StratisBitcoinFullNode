using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Tests.Common.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class UnrestrictedSmartContractTests : IClassFixture<PoAMockChainFixture>
    {
        private readonly IMockChain mockChain;
        private readonly MockChainNode node1;
        private readonly MockChainNode node2;

        public UnrestrictedSmartContractTests(PoAMockChainFixture fixture)
        {
            this.mockChain = fixture.Chain;
            this.node1 = this.mockChain.Nodes[0];
            this.node2 = this.mockChain.Nodes[1];
        }

        [Fact]
        public void DeployAndCallApiContract()
        {
            // Ensure fixture is funded.
            this.mockChain.MineBlocks(1);

            decimal amount = 25;

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("UnrestrictedSmartContracts/HitAnApiContract.cs");
            Assert.True(compilationResult.Success);

            const string apiUrl = "http://google.com";

            string[] parameters = new string[]
            {
                string.Format("{0}#{1}", (int)MethodParameterDataType.String, apiUrl),
            };
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount, parameters);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);

            var receipt = this.node1.GetReceipt(response.TransactionId.ToString());
        }
    }
}
