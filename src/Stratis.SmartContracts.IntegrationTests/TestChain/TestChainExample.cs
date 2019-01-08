using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Local;
using Stratis.SmartContracts.Test;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests.TestChain
{
    public class TestChainExample
    {
        [Fact]
        public void TestChain_Auction()
        {
            // Compile the contract we want to deploy
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/Auction.cs");
            Assert.True(compilationResult.Success);

            using (Test.TestChain chain = new Test.TestChain().Initialize())
            {
                // Get an address we can use for deploying
                Base58Address deployerAddress = chain.PreloadedAddresses[0];

                // Create and send transaction to mempool with parameters
                SendCreateContractResult createResult = chain.SendCreateContractTransaction(deployerAddress, compilationResult.Compilation, 0, new object[] {20uL});

                // Mine a block which will contain our sent transaction
                chain.MineBlocks(1);

                // Check the receipt to see that contract deployment was successful
                ReceiptResponse receipt = chain.GetReceipt(createResult.TransactionId);
                Assert.Equal(deployerAddress, receipt.From);

                // Check that the code is indeed saved on-chain
                byte[] savedCode = chain.GetCode(createResult.NewContractAddress);
                Assert.NotNull(savedCode);

                // Use another identity to bid
                Base58Address bidderAddress = chain.PreloadedAddresses[1];

                // Send a call to the bid method
                SendCallContractResult callResult = chain.SendCallContractTransaction(bidderAddress, "Bid", createResult.NewContractAddress, 1);
                chain.MineBlocks(1);

                // Call a method locally to check the state is as expected
                ILocalExecutionResult localCallResult = chain.CallContractMethodLocally(bidderAddress, "HighestBidder", createResult.NewContractAddress, 0);
                Address storedHighestBidder = (Address) localCallResult.Return;
                Assert.NotEqual(default(Address), storedHighestBidder); // TODO: A nice way of comparing hex and base58 representations
            }
        }
    }
}
