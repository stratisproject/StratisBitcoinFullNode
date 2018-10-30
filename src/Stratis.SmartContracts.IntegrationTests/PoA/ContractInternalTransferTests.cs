using System;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Stratis.SmartContracts.IntegrationTests.MockChain;
using Stratis.SmartContracts.IntegrationTests.PoA.MockChain;
using Stratis.SmartContracts.IntegrationTests.PoW.MockChain;
using Xunit;
using Block = NBitcoin.Block;

namespace Stratis.SmartContracts.IntegrationTests.PoA
{
    public class ContractInternalTransferTests : IClassFixture<PoAMockChainFixture>
    {
        private readonly PoAMockChain mockChain;
        private readonly MockChainNode node1;
        private readonly MockChainNode node2;

        private readonly IAddressGenerator addressGenerator;
        private readonly ISenderRetriever senderRetriever;

        public ContractInternalTransferTests(PoAMockChainFixture fixture)
        {
            this.mockChain = fixture.Chain;
            this.node1 = this.mockChain.Nodes[0];
            this.node2 = this.mockChain.Nodes[1];

            this.addressGenerator = new AddressGenerator();
            this.senderRetriever = new SenderRetriever();
        }

        [Fact]
        public void InternalTransfer_ToWalletAddress()
        {
            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/BasicTransfer.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse preResponse = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
            this.node1.WaitMempoolCount(1);
            this.node1.WaitForBlocksToBeMined(1);
            Assert.NotNull(this.node1.GetCode(preResponse.NewContractAddress));

            double amount = 25;

            // Send amount to contract, which will send to wallet address (address without code)
            uint160 walletUint160 = new uint160(1);
            string address = walletUint160.ToAddress().ToString();
            string[] parameters = new string[] { string.Format("{0}#{1}", (int)MethodParameterDataType.Address, address) };
            BuildCallContractTransactionResponse response = this.node1.SendCallContractTransaction(
                nameof(BasicTransfer.SendToAddress),
                preResponse.NewContractAddress,
                amount,
                parameters);
            this.node2.WaitMempoolCount(1);
            this.node2.WaitForBlocksToBeMined(1);

            // Contract doesn't maintain any balance
            Assert.Equal((ulong)0, this.node1.GetContractBalance(preResponse.NewContractAddress));

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs); // TODO: Could add logs to this test
            Assert.True(receipt.Success);
            Assert.True(receipt.GasUsed > GasPriceList.BaseCost);
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.Null(receipt.Error);
            Assert.Equal(preResponse.NewContractAddress, receipt.To);
        }
    }
}
