using System;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Consensus;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.IntegrationTests.MockChain;
using Xunit;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Block = NBitcoin.Block;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class ContractInternalTransferTests : IClassFixture<MockChainFixture>
    {
        private readonly Chain mockChain;
        private readonly Node node1;
        private readonly Node node2;

        private readonly ISenderRetriever senderRetriever;
        private readonly IAddressGenerator addressGenerator;

        public ContractInternalTransferTests(MockChainFixture fixture)
        {
            this.mockChain = fixture.Chain;
            this.node1 = this.mockChain.Nodes[0];
            this.node2 = this.mockChain.Nodes[1];

            this.senderRetriever = new SenderRetriever();
            this.addressGenerator = new AddressGenerator();
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
            string[] parameters = new string[] { string.Format("{0}#{1}", (int)MethodParameterDataType.Address, address) };
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

        [Fact]
        public void InternalTransfer_ToContractAddress()
        {
            // Ensure fixture is funded.
            this.node1.MineBlocks(1);

            // Deploy contract to send to
            ContractCompilationResult receiveCompilationResult = ContractCompiler.CompileFile("SmartContracts/BasicReceive.cs");
            Assert.True(receiveCompilationResult.Success);
            BuildCreateContractTransactionResponse receiveResponse = this.node1.SendCreateContractTransaction(receiveCompilationResult.Compilation, 0);
            this.node1.WaitMempoolCount(1);
            this.node1.MineBlocks(1);
            Assert.NotNull(this.node1.GetCode(receiveResponse.NewContractAddress));

            // Deploy contract to send from
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/BasicTransfer.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse preResponse = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
            this.node1.WaitMempoolCount(1);
            this.node1.MineBlocks(1);
            Assert.NotNull(this.node1.GetCode(preResponse.NewContractAddress));

            double amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            // Send amount to contract, which will send to contract address
            string[] parameters = new string[] { string.Format("{0}#{1}", (int)MethodParameterDataType.Address, receiveResponse.NewContractAddress) };
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

            // Contract doesn't maintain any balance
            Assert.Equal((ulong)0, this.node1.GetContractBalance(preResponse.NewContractAddress));

            // Receiver contract now has balance
            Assert.Equal((ulong) new Money((int) amount, MoneyUnit.BTC), this.node1.GetContractBalance(receiveResponse.NewContractAddress));

            // Receiver contract stored to state
            Assert.Equal(new byte[]{1}, this.node1.GetStorageValue(receiveResponse.NewContractAddress, BasicReceive.ReceiveKey));

            // Log was stored - bloom filter should be non-zero
            Assert.NotEqual(new Bloom(), ((SmartContractBlockHeader) lastBlock.Header).LogsBloom);

            // Block contains a condensing transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction condensingTransaction = lastBlock.Transactions[2];
            Assert.Single(condensingTransaction.Outputs); // Entire balance was forwarded
            byte[] toBytes = condensingTransaction.Outputs[0].ScriptPubKey.ToBytes();
            Assert.Equal((byte)ScOpcodeType.OP_INTERNALCONTRACTTRANSFER, toBytes[0]);
            uint160 toAddress = new uint160(toBytes.Skip(1).ToArray());
            Assert.Equal(receiveResponse.NewContractAddress, toAddress.ToAddress(this.mockChain.Network).Value);
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), condensingTransaction.Outputs[0].Value);
            Money fee = lastBlock.Transactions[0].Outputs[0].Value - new Money(50, MoneyUnit.BTC);

            // Amount in wallet is reduced by amount sent and fee.
            Assert.Equal(senderBalanceBefore - this.node1.WalletSpendableBalance, fee + new Money((long)amount, MoneyUnit.BTC));

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Single(receipt.Logs);
            Assert.Equal(receiveResponse.NewContractAddress, receipt.Logs[0].Address);
            Assert.True(receipt.Success);
            Assert.True(receipt.GasUsed > GasPriceList.BaseCost);
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.Null(receipt.Error);
            Assert.Equal(preResponse.NewContractAddress, receipt.To);
        }

        [Fact]
        public void InternalTransfer_FromConstructor()
        {
            // Ensure fixture is funded.
            this.node1.MineBlocks(1);

            double amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TransferFromConstructor.cs");
            Assert.True(compilationResult.Success);
            uint160 walletUint160 = new uint160(1);
            string address = walletUint160.ToAddress(this.mockChain.Network);
            string[] parameters = new string[] { string.Format("{0}#{1}", (int)MethodParameterDataType.Address, address) };
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount, parameters);
            this.node2.WaitMempoolCount(1);
            this.node2.MineBlocks(1);
            Assert.NotNull(this.node1.GetCode(response.NewContractAddress));
            Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Block contains a condensing transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction condensingTransaction = lastBlock.Transactions[2];
            Assert.Equal(2, condensingTransaction.Outputs.Count);

            // 1 output which is contract maintaining its balance
            byte[] toBytes = condensingTransaction.Outputs[0].ScriptPubKey.ToBytes();
            Assert.Equal((byte)ScOpcodeType.OP_INTERNALCONTRACTTRANSFER, toBytes[0]);
            uint160 toAddress = new uint160(toBytes.Skip(1).ToArray());
            Assert.Equal(response.NewContractAddress, toAddress.ToAddress(this.mockChain.Network).Value);
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC) / 2, condensingTransaction.Outputs[1].Value);

            // 1 output to address sent in params
            uint160 transferReceiver = this.senderRetriever.GetAddressFromScript(condensingTransaction.Outputs[1].ScriptPubKey).Sender;
            Assert.Equal(walletUint160, transferReceiver);
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC) / 2, condensingTransaction.Outputs[1].Value);
            Money fee = lastBlock.Transactions[0].Outputs[0].Value - new Money(50, MoneyUnit.BTC);

            // Amount in wallet is reduced by amount sent and fee.
            Assert.Equal(senderBalanceBefore - this.node1.WalletSpendableBalance, fee + new Money((long)amount, MoneyUnit.BTC));

            // Contract maintains half the balance
            Assert.Equal((ulong) new Money((long)amount, MoneyUnit.BTC) / 2, this.node1.GetContractBalance(response.NewContractAddress));

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs); // TODO: Could add logs to this test
            Assert.True(receipt.Success);
            Assert.True(receipt.GasUsed > GasPriceList.BaseCost);
            Assert.Equal(response.NewContractAddress, receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.Null(receipt.Error);
            Assert.Null(receipt.To);
        }

        [Fact(Skip = "TODO")]
        public void InternalTransfer_BetweenContracts()
        {
            //Method calls back and forth between 2 contracts
        }

        [Fact]
        public void InternalTransfer_BetweenContracts_FromConstructor()
        {
            // Ensure fixture is funded.
            this.node1.MineBlocks(1);

            double amount = 25;

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/MultipleNestedCalls.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount);
            this.node1.WaitMempoolCount(1);
            this.node1.MineBlocks(1);
            Assert.NotNull(this.node1.GetCode(response.NewContractAddress));

            Assert.NotNull(this.node1.GetStorageValue(response.NewContractAddress, "Caller"));
        }

        [Fact]
        public void InternalTransfer_Create_WithValueTransfer()
        {
            // Ensure fixture is funded.
            this.node1.MineBlocks(1);

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/CreationTransfer.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse preResponse = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
            this.node1.WaitMempoolCount(1);
            this.node1.MineBlocks(1);
            Assert.NotNull(this.node1.GetCode(preResponse.NewContractAddress));

            double amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            // Send amount to contract, which will send to new address of contract it creates
            BuildCallContractTransactionResponse response = this.node1.SendCallContractTransaction(
                nameof(CreationTransfer.CreateAnotherContract),
                preResponse.NewContractAddress,
                amount);
            this.node2.WaitMempoolCount(1);
            this.node2.MineBlocks(1);

            Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Get created contract address - TODO FIX
            uint160 createdAddress = this.addressGenerator.GenerateAddress(response.TransactionId, 0);

            // Block contains a condensing transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction condensingTransaction = lastBlock.Transactions[2];
            Assert.Single(condensingTransaction.Outputs); // Entire balance was forwarded,
            byte[] toBytes = condensingTransaction.Outputs[0].ScriptPubKey.ToBytes();
            Assert.Equal((byte)ScOpcodeType.OP_INTERNALCONTRACTTRANSFER, toBytes[0]);
            uint160 toAddress = new uint160(toBytes.Skip(1).ToArray());
            Assert.Equal(createdAddress, toAddress);
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), condensingTransaction.Outputs[0].Value);
            Money fee = lastBlock.Transactions[0].Outputs[0].Value - new Money(50, MoneyUnit.BTC);

            // Amount in wallet is reduced by amount sent and fee.
            Assert.Equal(senderBalanceBefore - this.node1.WalletSpendableBalance, fee + new Money((long)amount, MoneyUnit.BTC));

            // Contract doesn't maintain any balance
            Assert.Equal((ulong)0, this.node1.GetContractBalance(preResponse.NewContractAddress));

            // Created contract received full amount
            Assert.Equal((ulong) new Money((ulong)amount, MoneyUnit.BTC), this.node1.GetContractBalance(createdAddress.ToAddress(this.mockChain.Network)));

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

        [Fact]
        public void InternalTransfer_CreateMultipleContracts_FromConstructor()
        {
            // Ensure fixture is funded.
            this.node1.MineBlocks(1);

            double amount = 25;

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/NonceTest.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount);
            this.node1.WaitMempoolCount(1);
            this.node1.MineBlocks(1);
            Assert.NotNull(this.node1.GetCode(response.NewContractAddress));
            
            // Check that there is code for nonces 1 and 3 (not 2, contract deployment should have failed).
            uint160 successAddress1 = this.addressGenerator.GenerateAddress(response.TransactionId, 1);
            uint160 failAddress = this.addressGenerator.GenerateAddress(response.TransactionId, 2);
            uint160 successAddress2 = this.addressGenerator.GenerateAddress(response.TransactionId, 3);
            Assert.NotNull(this.node1.GetCode(successAddress1.ToAddress(this.mockChain.Network)));
            Assert.Null(this.node1.GetCode(failAddress.ToAddress(this.mockChain.Network)));
            Assert.NotNull(this.node1.GetCode(successAddress2.ToAddress(this.mockChain.Network)));

            Assert.Equal((ulong) 1, this.node1.GetContractBalance(successAddress1.ToAddress(this.mockChain.Network)));
            Assert.Equal((ulong) 0, this.node1.GetContractBalance(failAddress.ToAddress(this.mockChain.Network)));
            Assert.Equal((ulong) 1, this.node1.GetContractBalance(successAddress2.ToAddress(this.mockChain.Network)));
        }

    }
}
