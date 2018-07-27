using System;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.IntegrationTests.Common.MockChain;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.SmartContracts
{
    public class ContractCreationTests
    {

        [Fact]
        public void Test_CatCreation()
        {
            using (MockChain chain = new MockChain(2))
            {
                MockChainNode sender = chain.Nodes[0];
                MockChainNode receiver = chain.Nodes[1];

                sender.MineBlocks(10);

                SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/ContractCreation.cs");
                Assert.True(compilationResult.Success);

                // Create contract and ensure code exists
                BuildCreateContractTransactionResponse response = sender.SendCreateContractTransaction(compilationResult.Compilation);
                receiver.WaitMempoolCount(1);
                receiver.MineBlocks(2);
                Assert.NotNull(receiver.GetCode(response.NewContractAddress));
                Assert.NotNull(sender.GetCode(response.NewContractAddress));

                // Call contract and ensure owner is now highest bidder
                BuildCallContractTransactionResponse callResponse = sender.SendCallContractTransaction("CreateCat", response.NewContractAddress, 0);
                receiver.WaitMempoolCount(1);
                receiver.MineBlocks(2);
                Assert.Equal(1, BitConverter.ToInt32(sender.GetStorageValue(response.NewContractAddress, "CatCounter")));
            }
        }
    }
}
