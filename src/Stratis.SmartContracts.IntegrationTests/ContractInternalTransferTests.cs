using System;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.IntegrationTests.MockChain;
using Xunit;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Util;
using Block = NBitcoin.Block;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class ContractInternalTransferTests : IClassFixture<MockChainFixture>
    {
        private readonly Chain mockChain;
        private readonly Node node1;
        private readonly Node node2;

        private readonly ISenderRetriever senderRetriever;

        public ContractInternalTransferTests(MockChainFixture fixture)
        {
            this.mockChain = fixture.Chain;
            this.node1 = this.mockChain.Nodes[0];
            this.node2 = this.mockChain.Nodes[1];

            this.senderRetriever = new SenderRetriever();
        }

        [Fact]
        public void InternalTransfer_ToWalletAddress()
        {
            // Ensure fixture is funded.
            this.node1.MineBlocks(1);

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/BasicTransfer.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse preResponse = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
            this.node1.WaitMempoolCount(1);
            this.node1.MineBlocks(1);
            Assert.NotNull(this.node1.GetCode(preResponse.NewContractAddress));

            double amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            // Send amount to contract, which will send to wallet address (address without code)
            uint160 walletUint160 = new uint160(1);
            string address = walletUint160.ToAddress(this.mockChain.Network);
            string[] parameters = new string[] { string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Address, address) };
            BuildCallContractTransactionResponse response = this.node1.SendCallContractTransaction(
                nameof(BasicTransfer.SendToAddress),
                preResponse.NewContractAddress,
                amount,
                parameters);
            this.node2.WaitMempoolCount(1);
            this.node2.MineBlocks(1);

            Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Block contains a condensing transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction condensingTransaction = lastBlock.Transactions[2];
            Assert.Single(condensingTransaction.Outputs); // Entire balance was forwarded,
            uint160 transferReceiver = this.senderRetriever.GetAddressFromScript(condensingTransaction.Outputs[0].ScriptPubKey).Sender;
            Assert.Equal(walletUint160, transferReceiver);
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), condensingTransaction.Outputs[0].Value);
            Money fee = lastBlock.Transactions[0].Outputs[0].Value - new Money(50, MoneyUnit.BTC);

            // Amount in wallet is reduced by amount sent and fee.
            Assert.Equal(senderBalanceBefore - this.node1.WalletSpendableBalance, fee + new Money((long)amount, MoneyUnit.BTC));

            // Contract doesn't maintain any balance
            Assert.Equal((ulong) 0, this.node1.GetContractBalance(preResponse.NewContractAddress));

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs); // TODO: Could add logs to this test
            Assert.True(receipt.Success);
            Assert.True(receipt.GasUsed > GasPriceList.BaseCost);
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.Null(receipt.Error);
            Assert.Equal(preResponse.NewContractAddress, receipt.To);
        }

        [Fact(Skip = "TODO")]
        public void InternalTransfer_ToContractAddress()
        {
            //Transfer to contract
        }

        [Fact(Skip = "TODO")]
        public void InternalTransfer_BetweenContracts()
        {
            //Method calls back and forth between 2 contracts
        }

        [Fact(Skip = "TODO")]
        public void InternalTransfer_FromConstructor()
        {
            //Transfer from constructor
        }

        [Fact(Skip = "TODO")]
        public void InternalTransfer_Create_WithValueTransfer()
        {
            //Create with value transfer
        }

    }
}
