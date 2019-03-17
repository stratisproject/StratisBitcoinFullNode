using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.Tests.Common.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests.PoW
{
    public class ValidationTests
    {
        [Fact]
        public void Validate_ConstructorLoop()
        {
            using (PoWMockChain chain = new PoWMockChain(2))
            {
                MockChainNode sender = chain.Nodes[0];
                MockChainNode receiver = chain.Nodes[1];

                sender.MineBlocks(1);

                var byteCode = ContractCompiler.CompileFile("SmartContracts/ConstructorLoop.cs").Compilation;

                // Create contract and ensure code exists
                BuildCreateContractTransactionResponse response = sender.SendCreateContractTransaction(byteCode, 0);
                receiver.WaitMempoolCount(1);
                receiver.MineBlocks(2);
                Assert.NotNull(receiver.GetCode(response.NewContractAddress));
                Assert.NotNull(sender.GetCode(response.NewContractAddress));
            }
        }
    }
}
