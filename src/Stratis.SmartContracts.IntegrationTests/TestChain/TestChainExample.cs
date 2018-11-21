using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Test;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests.TestChain
{
    public class TestChainExample
    {
        [Fact]
        public void TestChain_Auction()
        {
            using (Test.TestChain chain = new Test.TestChain().Initialize())
            {
                Base58Address deployerAddress = chain.PreloadedAddresses[0];
                Assert.True(chain.GetBalanceInStratoshis(deployerAddress) > 0);

                ContractCompilationResult result = ContractCompiler.CompileFile("SmartContracts/Auction.cs");
                Assert.True(result.Success);
                SendCreateContractResult createResult = chain.SendCreateContractTransaction(deployerAddress, result.Compilation, 0, new object[] {20uL});
                chain.MineBlocks(1);
                ReceiptResponse receipt = chain.GetReceipt(createResult.TransactionId);
                Assert.Equal(deployerAddress, receipt.From);

            }
        }
    }
}
