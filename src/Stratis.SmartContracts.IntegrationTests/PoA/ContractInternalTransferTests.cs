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
            string address = walletUint160.ToBase58Address(this.mockChain.Network);
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

        [Fact]
        public void InternalTransfer_ToContractAddress()
        {
            // Deploy contract to send to
            ContractCompilationResult receiveCompilationResult = ContractCompiler.CompileFile("SmartContracts/BasicReceive.cs");
            Assert.True(receiveCompilationResult.Success);
            BuildCreateContractTransactionResponse receiveResponse = this.node1.SendCreateContractTransaction(receiveCompilationResult.Compilation, 0);
            this.node1.WaitMempoolCount(1);
            this.node1.WaitForBlocksToBeMined(1);
            Assert.NotNull(this.node1.GetCode(receiveResponse.NewContractAddress));

            // Deploy contract to send from
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/BasicTransfer.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse preResponse = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
            this.node1.WaitMempoolCount(1);
            this.node1.WaitForBlocksToBeMined(1);
            Assert.NotNull(this.node1.GetCode(preResponse.NewContractAddress));

            double amount = 25;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            // Send amount to contract, which will send to contract address
            string[] parameters = new string[] { string.Format("{0}#{1}", (int)MethodParameterDataType.Address, receiveResponse.NewContractAddress) };
            BuildCallContractTransactionResponse response = this.node1.SendCallContractTransaction(
                nameof(BasicTransfer.SendToAddress),
                preResponse.NewContractAddress,
                amount,
                parameters);
            this.node2.WaitMempoolCount(1);
            this.node2.WaitForBlocksToBeMined(1);

            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Contract doesn't maintain any balance
            Assert.Equal((ulong)0, this.node1.GetContractBalance(preResponse.NewContractAddress));

            // Receiver contract now has balance
            Assert.Equal((ulong)new Money((int)amount, MoneyUnit.BTC), this.node1.GetContractBalance(receiveResponse.NewContractAddress));

            // Receiver contract stored to state
            Assert.Equal(new byte[] { 1 }, this.node1.GetStorageValue(receiveResponse.NewContractAddress, BasicReceive.ReceiveKey));

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
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
            double amount = 25;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TransferFromConstructor.cs");
            Assert.True(compilationResult.Success);
            uint160 walletUint160 = new uint160(1);
            string address = walletUint160.ToBase58Address(this.mockChain.Network);
            string[] parameters = new string[] { string.Format("{0}#{1}", (int)MethodParameterDataType.Address, address) };
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount, parameters);
            this.node2.WaitMempoolCount(1);
            this.node2.WaitForBlocksToBeMined(1);
            Assert.NotNull(this.node1.GetCode(response.NewContractAddress));
            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Contract maintains half the balance
            Assert.Equal((ulong)new Money((long)amount, MoneyUnit.BTC) / 2, this.node1.GetContractBalance(response.NewContractAddress));

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs); // TODO: Could add logs to this test
            Assert.True(receipt.Success);
            Assert.True(receipt.GasUsed > GasPriceList.BaseCost);
            Assert.Equal(response.NewContractAddress, receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.Null(receipt.Error);
            Assert.Null(receipt.To);
        }

        [Fact]
        public void InternalTransfer_BetweenContracts()
        {
            // Deploy contract to send to
            ContractCompilationResult receiveCompilationResult = ContractCompiler.CompileFile("SmartContracts/NestedCallsReceiver.cs");
            Assert.True(receiveCompilationResult.Success);
            BuildCreateContractTransactionResponse receiveResponse = this.node1.SendCreateContractTransaction(receiveCompilationResult.Compilation, 0);
            this.node1.WaitMempoolCount(1);
            this.node1.WaitForBlocksToBeMined(1);
            Assert.NotNull(this.node1.GetCode(receiveResponse.NewContractAddress));

            // Deploy contract to send from
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/NestedCallsStarter.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse preResponse = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
            this.node1.WaitMempoolCount(1);
            this.node1.WaitForBlocksToBeMined(1);
            Assert.NotNull(this.node1.GetCode(preResponse.NewContractAddress));

            double amount = 25;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();
            string[] parameters = new string[]
            {
                string.Format("{0}#{1}", (int)MethodParameterDataType.Address, receiveResponse.NewContractAddress)
            };

            BuildCallContractTransactionResponse response = this.node1.SendCallContractTransaction(nameof(NestedCallsStarter.Start), preResponse.NewContractAddress, amount, parameters);
            this.node2.WaitMempoolCount(1);
            this.node2.WaitForBlocksToBeMined(1);
            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Storage set correctly
            Assert.Equal(BitConverter.GetBytes(NestedCallsStarter.Return), this.node1.GetStorageValue(preResponse.NewContractAddress, NestedCallsStarter.Key));

            // Received 1/2 the sent funds + 1/2 of those funds
            Money transferAmount1 = new Money((long)amount, MoneyUnit.BTC) / 2;
            Money transferAmount2 = new Money((long)amount, MoneyUnit.BTC) / 4;
            Assert.Equal((ulong)(transferAmount1 + transferAmount2), this.node1.GetContractBalance(preResponse.NewContractAddress));

            // Received 1/2 the sent funds, but sent 1/2 of those funds back
            Assert.Equal((ulong)(new Money((long)amount, MoneyUnit.BTC) - (transferAmount1 + transferAmount2)), this.node1.GetContractBalance(receiveResponse.NewContractAddress));
        }

        [Fact]
        public void InternalTransfer_BetweenContracts_FromConstructor()
        {
            double amount = 25;

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/MultipleNestedCalls.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount);
            this.node1.WaitMempoolCount(1);
            this.node1.WaitForBlocksToBeMined(1);
            Assert.NotNull(this.node1.GetCode(response.NewContractAddress));
            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Storage from nested call succeeded
            Assert.NotNull(this.node1.GetStorageValue(response.NewContractAddress, "Caller"));

            Assert.Equal((ulong)new Money((long)amount, MoneyUnit.BTC) / 2, this.node1.GetContractBalance(response.NewContractAddress));

            // 1 output to other deployed contract
            uint160 internalContract = this.addressGenerator.GenerateAddress(response.TransactionId, 1);
            Assert.Equal((ulong)new Money((long)amount, MoneyUnit.BTC) / 2, this.node1.GetContractBalance(internalContract.ToBase58Address(this.mockChain.Network)));
        }

        [Fact]
        public void InternalTransfer_Create_WithValueTransfer()
        {
            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/CreationTransfer.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse preResponse = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
            this.node1.WaitMempoolCount(1);
            this.node1.WaitForBlocksToBeMined(1);
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
            this.node2.WaitForBlocksToBeMined(1);

            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Get created contract address - TODO FIX
            uint160 createdAddress = this.addressGenerator.GenerateAddress(response.TransactionId, 0);

            // Contract doesn't maintain any balance
            Assert.Equal((ulong)0, this.node1.GetContractBalance(preResponse.NewContractAddress));

            // Created contract received full amount
            Assert.Equal((ulong)new Money((ulong)amount, MoneyUnit.BTC), this.node1.GetContractBalance(createdAddress.ToBase58Address(this.mockChain.Network)));

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

        [Fact]
        public void InternalTransfer_CreateMultipleContracts_FromConstructor_NonceIncreases()
        {
            double amount = 25;

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/NonceTest.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount);
            this.node1.WaitMempoolCount(1);
            this.node1.WaitForBlocksToBeMined(1);
            Assert.NotNull(this.node1.GetCode(response.NewContractAddress));

            // Check that there is code for nonces 1 and 3 (not 2, contract deployment should have failed).
            uint160 successAddress1 = this.addressGenerator.GenerateAddress(response.TransactionId, 1);
            uint160 failAddress = this.addressGenerator.GenerateAddress(response.TransactionId, 2);
            uint160 successAddress2 = this.addressGenerator.GenerateAddress(response.TransactionId, 3);
            Assert.NotNull(this.node1.GetCode(successAddress1.ToBase58Address(this.mockChain.Network)));
            Assert.Null(this.node1.GetCode(failAddress.ToBase58Address(this.mockChain.Network)));
            Assert.NotNull(this.node1.GetCode(successAddress2.ToBase58Address(this.mockChain.Network)));

            Assert.Equal((ulong)1, this.node1.GetContractBalance(successAddress1.ToBase58Address(this.mockChain.Network)));
            Assert.Equal((ulong)0, this.node1.GetContractBalance(failAddress.ToBase58Address(this.mockChain.Network)));
            Assert.Equal((ulong)1, this.node1.GetContractBalance(successAddress2.ToBase58Address(this.mockChain.Network)));
        }

        [Fact]
        public void ExternalTransfer_ReceiveHandler_WithValue()
        {
            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ReceiveFundsTest.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
            this.node2.WaitMempoolCount(1);
            this.node2.WaitForBlocksToBeMined(1);
            Assert.NotNull(this.node1.GetCode(response.NewContractAddress));
            uint160 contractAddress = this.addressGenerator.GenerateAddress(response.TransactionId, 0);

            ulong amount = 123;

            BuildCallContractTransactionResponse callResponse = this.node1.SendCallContractTransaction(
                MethodCall.ReceiveHandlerName,
                response.NewContractAddress,
                amount);
            this.node2.WaitMempoolCount(1);
            this.node2.WaitForBlocksToBeMined(1);

            // Stored balance in PersistentState should be only that which was sent
            byte[] saved = this.node1.GetStorageValue(contractAddress.ToBase58Address(this.mockChain.Network), "ReceiveBalance");
            ulong savedUlong = BitConverter.ToUInt64(saved);
            Assert.True((new Money(amount, MoneyUnit.BTC) == new Money(savedUlong, MoneyUnit.Satoshi)));
        }

        [Fact]
        public void ExternalTransfer_Create_WithValueTransfer()
        {
            ulong amount = 25;

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ReceiveFundsTest.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount);
            this.node2.WaitMempoolCount(1);
            this.node2.WaitForBlocksToBeMined(1);
            Assert.NotNull(this.node1.GetCode(response.NewContractAddress));
            uint160 contractAddress = this.addressGenerator.GenerateAddress(response.TransactionId, 0);

            // Stored balance in PersistentState should be only that which was sent
            byte[] saved = this.node1.GetStorageValue(contractAddress.ToBase58Address(this.mockChain.Network), "Balance");
            ulong savedUlong = BitConverter.ToUInt64(saved);
            Assert.True((new Money(amount, MoneyUnit.BTC) == new Money(savedUlong, MoneyUnit.Satoshi)));
        }

        [Fact]
        public void InternalTransfer_Nested_Create_Balance_Correct()
        {
            double amount = 25;

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/BalanceTest.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount);
            this.node2.WaitMempoolCount(1);
            this.node2.WaitForBlocksToBeMined(1);
            Assert.NotNull(this.node1.GetCode(response.NewContractAddress));
            uint160 internalContract = this.addressGenerator.GenerateAddress(response.TransactionId, 1);

            // Stored balance in PersistentState should be only that which was sent (10)
            byte[] saved = this.node1.GetStorageValue(internalContract.ToBase58Address(this.mockChain.Network), "Balance");
            ulong savedUlong = BitConverter.ToUInt64(saved);
            Assert.Equal((ulong)10, savedUlong);
        }

        [Fact]
        public void External_Call_Balance_Correct()
        {
            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ReceiveFundsTest.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
            this.node2.WaitMempoolCount(1);
            this.node2.WaitForBlocksToBeMined(1);
            Assert.NotNull(this.node1.GetCode(response.NewContractAddress));
            uint160 contractAddress = this.addressGenerator.GenerateAddress(response.TransactionId, 0);

            ulong amount = 123;

            BuildCallContractTransactionResponse callResponse = this.node1.SendCallContractTransaction(
                nameof(ReceiveFundsTest.MethodReceiveFunds),
                response.NewContractAddress,
                amount);
            this.node2.WaitMempoolCount(1);
            this.node2.WaitForBlocksToBeMined(1);

            // Stored balance in PersistentState should be only that which was sent
            byte[] saved = this.node1.GetStorageValue(contractAddress.ToBase58Address(this.mockChain.Network), "Balance");
            ulong savedUlong = BitConverter.ToUInt64(saved);
            Assert.True((new Money(amount, MoneyUnit.BTC) == new Money(savedUlong, MoneyUnit.Satoshi)));
        }

        [Fact]
        public void Internal_Nested_Transfer_Balance_Correct()
        {
            double amount = 25;

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ReceiveFundsTest.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount);
            this.node2.WaitMempoolCount(1);
            this.node2.WaitForBlocksToBeMined(1);
            Assert.NotNull(this.node1.GetCode(response.NewContractAddress));

            // Deploy second contract
            BuildCreateContractTransactionResponse response2 = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
            this.node2.WaitMempoolCount(1);
            this.node2.WaitForBlocksToBeMined(1);
            Assert.NotNull(this.node1.GetCode(response2.NewContractAddress));
            uint160 contract2Address = this.addressGenerator.GenerateAddress(response2.TransactionId, 0);

            ulong transferredAmount = 123;

            string[] parameters = new string[]
            {
                string.Format("{0}#{1}", (int)MethodParameterDataType.Address, response2.NewContractAddress),
                string.Format("{0}#{1}", (int)MethodParameterDataType.ULong, transferredAmount)
            };

            // Invoke method on contract1 that calls contract2 and check that balance is correct
            BuildCallContractTransactionResponse callResponse = this.node1.SendCallContractTransaction(
                nameof(ReceiveFundsTest.TransferFunds),
                response.NewContractAddress,
                0,
                parameters);
            this.node2.WaitMempoolCount(1);
            this.node2.WaitForBlocksToBeMined(1);

            // Stored balance in PersistentState should be only that which was sent
            byte[] saved = this.node1.GetStorageValue(contract2Address.ToBase58Address(this.mockChain.Network), "ReceiveBalance");
            ulong savedUlong = BitConverter.ToUInt64(saved);
            Assert.Equal(transferredAmount, savedUlong);
        }

        [Fact]
        public void Internal_Nested_Call_Transfer_To_Self_Balance_Correct()
        {
            ulong amount = 25;

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ReceiveFundsTest.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount);
            this.node2.WaitMempoolCount(1);
            this.node2.WaitForBlocksToBeMined(1);
            Assert.NotNull(this.node1.GetCode(response.NewContractAddress));
            uint160 contract1Address = this.addressGenerator.GenerateAddress(response.TransactionId, 0);

            ulong transferredAmount = 123;

            string[] parameters = new string[]
            {
                string.Format("{0}#{1}", (int)MethodParameterDataType.Address, response.NewContractAddress),
                string.Format("{0}#{1}", (int)MethodParameterDataType.ULong, transferredAmount)
            };

            // Invoke call which sends 123 to self. Balance should remain the same.
            BuildCallContractTransactionResponse callResponse = this.node1.SendCallContractTransaction(
                nameof(ReceiveFundsTest.TransferFunds),
                response.NewContractAddress,
                0,
                parameters);
            this.node2.WaitMempoolCount(1);
            this.node2.WaitForBlocksToBeMined(1);

            // Stored balance in PersistentState should be only that which was sent
            byte[] saved = this.node1.GetStorageValue(contract1Address.ToBase58Address(this.mockChain.Network), "ReceiveBalance");
            ulong savedUlong = BitConverter.ToUInt64(saved);

            // Balance should be the same as the initial amount
            Assert.True((new Money(amount, MoneyUnit.BTC) == new Money(savedUlong, MoneyUnit.Satoshi)));
        }
    }
}
