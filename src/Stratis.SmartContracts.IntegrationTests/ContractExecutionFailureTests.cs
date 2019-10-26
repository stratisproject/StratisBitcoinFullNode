using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpFunctionalExtensions;
using Mono.Cecil;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.RuntimeObserver;
using Stratis.SmartContracts.Tests.Common.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public abstract class ContractExecutionFailureTests<T> : IClassFixture<T> where T : class, IMockChainFixture
    {
        private readonly IMockChain mockChain;
        private readonly MockChainNode node1;
        private readonly MockChainNode node2;

        private readonly IAddressGenerator addressGenerator;
        private readonly ISenderRetriever senderRetriever;

        protected ContractExecutionFailureTests(T fixture)
        {
            this.mockChain = fixture.Chain;
            this.node1 = this.mockChain.Nodes[0];
            this.node2 = this.mockChain.Nodes[1];
            this.addressGenerator = new AddressGenerator();
            this.senderRetriever = new SenderRetriever();
        }

        [Fact]
        public void ContractTransaction_Invalid_MethodParamSerialization()
        {
            // Create poorly serialized method params
            var serializer =
                new CallDataSerializer(new ContractPrimitiveSerializer(this.node1.CoreNode.FullNode.Network));

            var txData = serializer.Serialize(new ContractTxData(1, 1, (RuntimeObserver.Gas)(GasPriceList.BaseCost + 1), new uint160(1), "Test"));

            var random = new Random();
            byte[] bytes = new byte[101];
            random.NextBytes(bytes);

            // Last 4 bytes are 0000. Remove and replace with garbage
            var garbageTxData = new byte[txData.Length - 4 + bytes.Length];

            txData.CopyTo(garbageTxData, 0);
            bytes.CopyTo(garbageTxData, txData.Length - 4);

            // Send fails - doesn't even make it to mempool
            Result<WalletSendTransactionModel> result = this.node1.SendTransaction(new Script(garbageTxData), 25);
            Assert.True(result.IsFailure);
            Assert.Equal("Invalid ContractTxData format", result.Error); // TODO: const error message
        }

        [Fact]
        public void EmptyMethodNameFails()
        {
            // Ensure fixture is funded.
            this.mockChain.MineBlocks(1);

            // Deploy contract to send to
            ContractCompilationResult receiveCompilationResult = ContractCompiler.CompileFile("SmartContracts/BasicReceive.cs");
            Assert.True(receiveCompilationResult.Success);
            BuildCreateContractTransactionResponse receiveResponse = this.node1.SendCreateContractTransaction(receiveCompilationResult.Compilation, 0);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            Assert.NotNull(this.node1.GetCode(receiveResponse.NewContractAddress));

            decimal amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            // Send to empty method name on contract
            string[] parameters = new string[] { string.Format("{0}#{1}", (int)MethodParameterDataType.Address, receiveResponse.NewContractAddress) };
            BuildCallContractTransactionResponse response = this.node1.SendCallContractTransaction(
                "",
                receiveResponse.NewContractAddress,
                amount,
                parameters);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);

            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Receipt shows failure
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.False(receipt.Success);
            Assert.Equal(StateTransitionErrors.NoMethodName, receipt.Error);
        }

        [Fact]
        public void ContractTransaction_InvalidSerialization()
        {
            // Create poorly serialized transaction
            var random = new Random();
            byte[] bytes = new byte[101];
            random.NextBytes(bytes);
            bytes[0] = (byte)ScOpcodeType.OP_CALLCONTRACT;

            // Send fails - doesn't even make it to mempool
            Result<WalletSendTransactionModel> result = this.node1.SendTransaction(new Script(bytes), 25);
            Assert.True(result.IsFailure);
            Assert.Equal("Invalid ContractTxData format", result.Error); // TODO: const error message
        }

        [Fact]
        public void ContractTransaction_InvalidByteCode()
        {
            // Ensure fixture is funded.
            this.mockChain.MineBlocks(1);

            decimal amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            // Create transaction with random bytecode.
            var random = new Random();
            byte[] bytes = new byte[100];
            random.NextBytes(bytes);
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(bytes, amount);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Contract wasn't created
            Assert.Null(this.node1.GetCode(response.NewContractAddress));

            // Block contains a refund transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction refundTransaction = lastBlock.Transactions[2];
            uint160 refundReceiver = this.senderRetriever.GetAddressFromScript(refundTransaction.Outputs[0].ScriptPubKey).Sender;
            Assert.Equal(this.node1.MinerAddress.Address, refundReceiver.ToBase58Address(this.node1.CoreNode.FullNode.Network));
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), refundTransaction.Outputs[0].Value);

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs);
            Assert.False(receipt.Success);
            Assert.Equal(GasPriceList.CreateCost, receipt.GasUsed);
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.Null(receipt.To);
        }

        [Fact]
        public void ContractTransaction_ValidationFailure()
        {
            // Ensure fixture is funded.
            this.mockChain.MineBlocks(1);

            decimal amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            // Create transaction with non-deterministic bytecode.
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/NonDeterministicContract.cs");
            Assert.True(compilationResult.Success);

            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Contract wasn't created
            Assert.Null(this.node2.GetCode(response.NewContractAddress));

            // Block contains a refund transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction refundTransaction = lastBlock.Transactions[2];
            uint160 refundReceiver = this.senderRetriever.GetAddressFromScript(refundTransaction.Outputs[0].ScriptPubKey).Sender;
            Assert.Equal(this.node1.MinerAddress.Address, refundReceiver.ToBase58Address(this.node1.CoreNode.FullNode.Network));
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), refundTransaction.Outputs[0].Value);

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs);
            Assert.False(receipt.Success);
            Assert.Equal(GasPriceList.CreateCost, receipt.GasUsed);
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.Null(receipt.To);
        }

        [Fact]
        public void ContractTransaction_ExceptionInCreate()
        {
            // Ensure fixture is funded.
            this.mockChain.MineBlocks(1);

            decimal amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ExceptionInConstructor.cs");
            Assert.True(compilationResult.Success);

            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Contract wasn't created
            Assert.Null(this.node2.GetCode(response.NewContractAddress));

            // State wasn't persisted
            Assert.Null(this.node2.GetStorageValue(response.NewContractAddress, "Test"));

            // Logs weren't persisted
            Assert.Equal(new Bloom(), ((ISmartContractBlockHeader)lastBlock.Header).LogsBloom);

            // Block contains a refund transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction refundTransaction = lastBlock.Transactions[2];
            Assert.Single(refundTransaction.Outputs); // No transfers persisted
            uint160 refundReceiver = this.senderRetriever.GetAddressFromScript(refundTransaction.Outputs[0].ScriptPubKey).Sender;
            Assert.Equal(this.node1.MinerAddress.Address, refundReceiver.ToBase58Address(this.node1.CoreNode.FullNode.Network));
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), refundTransaction.Outputs[0].Value);

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs);
            Assert.False(receipt.Success);
            Assert.True(receipt.GasUsed > GasPriceList.BaseCost);
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.StartsWith("System.IndexOutOfRangeException", receipt.Error);
            Assert.Null(receipt.To);
        }

        [Fact]
        public void ContractTransaction_ExceptionInCall()
        {
            // Ensure fixture is funded.
            this.mockChain.MineBlocks(1);

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ExceptionInMethod.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse preResponse = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            Assert.NotNull(this.node1.GetCode(preResponse.NewContractAddress));

            decimal amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            BuildCallContractTransactionResponse response = this.node1.SendCallContractTransaction("Method", preResponse.NewContractAddress, amount);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // State wasn't persisted
            Assert.Null(this.node2.GetStorageValue(preResponse.NewContractAddress, "Test"));

            // Logs weren't persisted
            Assert.Equal(new Bloom(), ((ISmartContractBlockHeader)lastBlock.Header).LogsBloom);

            // Block contains a refund transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction refundTransaction = lastBlock.Transactions[2];
            Assert.Single(refundTransaction.Outputs); // No transfers persisted
            uint160 refundReceiver = this.senderRetriever.GetAddressFromScript(refundTransaction.Outputs[0].ScriptPubKey).Sender;
            Assert.Equal(this.node1.MinerAddress.Address, refundReceiver.ToBase58Address(this.node1.CoreNode.FullNode.Network));
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), refundTransaction.Outputs[0].Value);

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs);
            Assert.False(receipt.Success);
            Assert.True(receipt.GasUsed > GasPriceList.BaseCost);
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.StartsWith("System.IndexOutOfRangeException", receipt.Error);
            Assert.Equal(preResponse.NewContractAddress, receipt.To);
        }

        [Fact]
        public void ContractTransaction_AddressDoesntExist()
        {
            // Ensure fixture is funded.
            this.mockChain.MineBlocks(1);

            decimal amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            // Send call to non-existent address
            string nonExistentAddress = new uint160(0).ToBase58Address(this.node1.CoreNode.FullNode.Network);
            Assert.Null(this.node1.GetCode(nonExistentAddress));
            BuildCallContractTransactionResponse response = this.node1.SendCallContractTransaction("Method", nonExistentAddress, amount);

            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Block contains a refund transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction refundTransaction = lastBlock.Transactions[2];
            Assert.Single(refundTransaction.Outputs); // No transfers
            uint160 refundReceiver = this.senderRetriever.GetAddressFromScript(refundTransaction.Outputs[0].ScriptPubKey).Sender;
            Assert.Equal(this.node1.MinerAddress.Address, refundReceiver.ToBase58Address(this.node1.CoreNode.FullNode.Network));
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), refundTransaction.Outputs[0].Value);

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs);
            Assert.False(receipt.Success);
            Assert.Equal(GasPriceList.BaseCost, receipt.GasUsed);
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.Equal(StateTransitionErrors.NoCode, receipt.Error);
            Assert.Equal(nonExistentAddress, receipt.To);
        }

        [Fact]
        public void ContractTransaction_MethodDoesntExist()
        {
            // Ensure fixture is funded.
            this.mockChain.MineBlocks(1);

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/EmptyContract.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse preResponse = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            Assert.NotNull(this.node1.GetCode(preResponse.NewContractAddress));

            decimal amount = 25;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            BuildCallContractTransactionResponse response = this.node1.SendCallContractTransaction("MethodThatDoesntExist", preResponse.NewContractAddress, amount);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Block contains a refund transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction refundTransaction = lastBlock.Transactions[2];
            Assert.Single(refundTransaction.Outputs); // No transfers persisted
            uint160 refundReceiver = this.senderRetriever.GetAddressFromScript(refundTransaction.Outputs[0].ScriptPubKey).Sender;
            Assert.Equal(this.node1.MinerAddress.Address, refundReceiver.ToBase58Address(this.node1.CoreNode.FullNode.Network));
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), refundTransaction.Outputs[0].Value);

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs);
            Assert.False(receipt.Success);
            Assert.Equal(GasPriceList.BaseCost, receipt.GasUsed);
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.Equal(ContractInvocationErrors.MethodDoesNotExist, receipt.Error);
            Assert.Equal(preResponse.NewContractAddress, receipt.To);
        }

        [Fact]
        public void ContractTransaction_CallPrivateMethod()
        {
            // Ensure fixture is funded.
            this.mockChain.MineBlocks(1);

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/PrivateMethod.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse preResponse = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            Assert.NotNull(this.node1.GetCode(preResponse.NewContractAddress));

            decimal amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            BuildCallContractTransactionResponse response = this.node1.SendCallContractTransaction("CallMe", preResponse.NewContractAddress, amount);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // State wasn't persisted
            Assert.Null(this.node2.GetStorageValue(preResponse.NewContractAddress, "Called"));

            // Logs weren't persisted
            Assert.Equal(new Bloom(), ((ISmartContractBlockHeader)lastBlock.Header).LogsBloom);

            // Block contains a refund transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction refundTransaction = lastBlock.Transactions[2];
            Assert.Single(refundTransaction.Outputs); // No transfers persisted
            uint160 refundReceiver = this.senderRetriever.GetAddressFromScript(refundTransaction.Outputs[0].ScriptPubKey).Sender;
            Assert.Equal(this.node1.MinerAddress.Address, refundReceiver.ToBase58Address(this.node1.CoreNode.FullNode.Network));
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), refundTransaction.Outputs[0].Value);

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs);
            Assert.False(receipt.Success);
            Assert.Equal(GasPriceList.BaseCost, receipt.GasUsed);
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.Equal(ContractInvocationErrors.MethodDoesNotExist, receipt.Error);
            Assert.Equal(preResponse.NewContractAddress, receipt.To);
        }

        [Fact]
        public void ContractTransaction_Create_IncorrectParameters()
        {
            // Ensure fixture is funded.
            this.mockChain.MineBlocks(1);

            decimal amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/BasicParameters.cs");
            Assert.True(compilationResult.Success);
            string[] parameters = new string[] { string.Format("{0}#{1}", (int)MethodParameterDataType.ULong, UInt64.MaxValue) };
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount, parameters);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Contract wasn't created
            Assert.Null(this.node2.GetCode(response.NewContractAddress));

            // State wasn't persisted
            Assert.Null(this.node2.GetStorageValue(response.NewContractAddress, "Created"));

            // Logs weren't persisted
            Assert.Equal(new Bloom(), ((ISmartContractBlockHeader)lastBlock.Header).LogsBloom);

            // Block contains a refund transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction refundTransaction = lastBlock.Transactions[2];
            Assert.Single(refundTransaction.Outputs); // No transfers persisted
            uint160 refundReceiver = this.senderRetriever.GetAddressFromScript(refundTransaction.Outputs[0].ScriptPubKey).Sender;
            Assert.Equal(this.node1.MinerAddress.Address, refundReceiver.ToBase58Address(this.node1.CoreNode.FullNode.Network));
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), refundTransaction.Outputs[0].Value);
            Money fee = lastBlock.Transactions[0].Outputs[0].Value - new Money(50, MoneyUnit.BTC);

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs);
            Assert.False(receipt.Success);
            Assert.Equal(GasPriceList.CreateCost, receipt.GasUsed);
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.StartsWith(ContractInvocationErrors.MethodDoesNotExist, receipt.Error); // The error for constructor not found vs method does not exist could be different in future.
            Assert.Null(receipt.To);
        }


        [Fact]
        public void ContractTransaction_EmptyModule_Failure()
        {
            // Ensure fixture is funded.
            this.mockChain.MineBlocks(1);

            decimal amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            // Create transaction with empty module
            var module = ModuleDefinition.CreateModule("SmartContract", ModuleKind.Dll);

            byte[] emptyModule;

            using (var ms = new MemoryStream())
            {
                module.Write(ms);
                emptyModule = ms.ToArray();
            }

            Assert.Single(module.Types);
            Assert.Equal("<Module>", module.Types[0].Name);

            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(emptyModule, amount);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Contract wasn't created
            Assert.Null(this.node2.GetCode(response.NewContractAddress));

            // Block contains a refund transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction refundTransaction = lastBlock.Transactions[2];
            uint160 refundReceiver = this.senderRetriever.GetAddressFromScript(refundTransaction.Outputs[0].ScriptPubKey).Sender;
            Assert.Equal(this.node1.MinerAddress.Address, refundReceiver.ToBase58Address(this.node1.CoreNode.FullNode.Network));
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), refundTransaction.Outputs[0].Value);
            Money fee = lastBlock.Transactions[0].Outputs[0].Value - new Money(50, MoneyUnit.BTC);

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs);
            Assert.False(receipt.Success);
            Assert.Equal(GasPriceList.CreateCost, receipt.GasUsed);
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.Null(receipt.To);
        }

        [Fact]
        public void ContractTransaction_RecursiveContractCreate_OutOfGas()
        {
            // Ensure fixture is funded.
            this.mockChain.MineBlocks(1);

            decimal amount = 25;
            ulong gasLimit = SmartContractFormatLogic.GasLimitMaximum;

            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/RecursiveLoopCreate.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount, gasLimit: gasLimit);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Contract was not created
            Assert.Null(this.node2.GetCode(response.NewContractAddress));

            // Block contains a refund transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction refundTransaction = lastBlock.Transactions[2];
            uint160 refundReceiver = this.senderRetriever.GetAddressFromScript(refundTransaction.Outputs[0].ScriptPubKey).Sender;
            Assert.Equal(this.node1.MinerAddress.Address, refundReceiver.ToBase58Address(this.node1.CoreNode.FullNode.Network));
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), refundTransaction.Outputs[0].Value);

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs);
            Assert.False(receipt.Success);
            Assert.True(receipt.GasUsed > (gasLimit - GasPriceList.BaseCost)); // The amount spent should be within 1 BaseCost of being used up.
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.Null(receipt.To);
        }

        [Fact]
        public void ContractTransaction_RecursiveContractCall_OutOfGas()
        {
            // Ensure fixture is funded.
            this.mockChain.MineBlocks(1);

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/RecursiveLoopCall.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse preResponse = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            Assert.NotNull(this.node1.GetCode(preResponse.NewContractAddress));

            decimal amount = 25;
            ulong gasLimit = SmartContractFormatLogic.GasLimitMaximum;

            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            BuildCallContractTransactionResponse response = this.node1.SendCallContractTransaction(nameof(RecursiveLoopCall.Call), preResponse.NewContractAddress, amount, gasLimit: gasLimit);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Block contains a refund transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction refundTransaction = lastBlock.Transactions[2];
            Assert.Single(refundTransaction.Outputs); // No transfers persisted
            uint160 refundReceiver = this.senderRetriever.GetAddressFromScript(refundTransaction.Outputs[0].ScriptPubKey).Sender;
            Assert.Equal(this.node1.MinerAddress.Address, refundReceiver.ToBase58Address(this.node1.CoreNode.FullNode.Network));
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), refundTransaction.Outputs[0].Value);

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs);
            Assert.False(receipt.Success);
            Assert.True(receipt.GasUsed > (gasLimit - GasPriceList.BaseCost)); // The amount spent should be within 1 BaseCost of being used up.
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.StartsWith("Stratis.SmartContracts.SmartContractAssertException", receipt.Error);
            Assert.Equal(preResponse.NewContractAddress, receipt.To);
        }

        [Fact]
        public void ContractTransaction_MemoryLimit()
        {
            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/MemoryConsumingContract.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse preResponse = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            Assert.NotNull(this.node1.GetCode(preResponse.NewContractAddress));

            decimal amount = 25;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            string[] parameters = new string[] { string.Format("{0}#{1}", (int)MethodParameterDataType.Int, 100001) };
            BuildCallContractTransactionResponse response = this.node1.SendCallContractTransaction("UseTooMuchMemory", preResponse.NewContractAddress, amount, parameters);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Block contains a refund transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction refundTransaction = lastBlock.Transactions[2];
            Assert.Single(refundTransaction.Outputs); // No transfers persisted
            uint160 refundReceiver = this.senderRetriever.GetAddressFromScript(refundTransaction.Outputs[0].ScriptPubKey).Sender;
            Assert.Equal(this.node1.MinerAddress.Address, refundReceiver.ToBase58Address(this.node1.CoreNode.FullNode.Network));
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), refundTransaction.Outputs[0].Value);
            Money fee = lastBlock.Transactions[0].Outputs[0].Value - new Money(50, MoneyUnit.BTC);

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs);
            Assert.False(receipt.Success);
            Assert.True(GasPriceList.BaseCost < receipt.GasUsed);
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.StartsWith($"{typeof(RuntimeObserver.MemoryConsumptionException).FullName}", receipt.Error);
            Assert.Equal(preResponse.NewContractAddress, receipt.To);
        }

        [Fact]
        public void ContractTransaction_Call_Method_Consumes_All_Gas_Throws_OutOfGasException()
        {
            // Ensure fixture is funded.
            this.mockChain.MineBlocks(1);

            // Deploy contract
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/InfiniteLoop.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse preResponse = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            Assert.NotNull(this.node1.GetCode(preResponse.NewContractAddress));

            decimal amount = 0;

            ulong gasLimit = SmartContractFormatLogic.GasLimitCallMinimum + 1;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            BuildCallContractTransactionResponse response = this.node1.SendCallContractTransaction(nameof(InfiniteLoop.Loop), preResponse.NewContractAddress, amount, gasLimit: gasLimit, gasPrice: 200);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);
            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Block does not contain a refund transaction
            Assert.Equal(2, lastBlock.Transactions.Count);

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs);
            Assert.False(receipt.Success);
            Assert.Equal(gasLimit, receipt.GasUsed); // All the gas should have been consumed
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.StartsWith("Execution ran out of gas.", receipt.Error);
            Assert.Equal(preResponse.NewContractAddress, receipt.To);
        }

        [Fact]
        public void ContractTransaction_Call_Method_Reach_Limit_Of_GasPerBlock_Transaction_NotIncluded_To_Block()
        {
            var gasPrice_100 = SmartContractMempoolValidator.MinGasPrice;
            var gasLimit_50_000 = (Gas)(SmartContractFormatLogic.GasLimitMaximum / 2);
            var txGasPerBlockLimit_1_000_000 = SmartContractFormatLogic.GasLimitMaximum * 10;
            var txCount_25 = 25;

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/InfiniteLoop.cs");
            Assert.True(compilationResult.Success);

            // Create the contract and mine it.
            BuildCreateContractTransactionResponse preResponse = this.node1.SendCreateContractTransaction(compilationResult.Compilation, 0, gasPrice: gasPrice_100);
            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);

            // Ensure that the contract is created.
            Assert.NotNull(this.node1.GetCode(preResponse.NewContractAddress));

            var contractTransactionIds = new List<uint256>();

            // Create 25 call contract transactions.
            for (int i = 0; i < txCount_25; i++)
            {
                BuildCallContractTransactionResponse response = this.node1.SendCallContractTransaction("Loop", preResponse.NewContractAddress, 0);
                if (!response.Success)
                    Assert.True(response.Success);

                contractTransactionIds.Add(response.TransactionId);
            }

            // Try and include the 25 transactions in the block (only 20 will be added).
            this.mockChain.WaitAllMempoolCount(txCount_25);
            this.mockChain.MineBlocks(1);

            NBitcoin.Block lastBlock = this.node1.GetLastBlock();

            // There should be 20 transactions in the last block as that is when the block gas expenditure limit was reached.
            // 19 included transactions plus 1 coinbase.
            int expectedTxCount_21 = Convert.ToInt32(txGasPerBlockLimit_1_000_000 / gasLimit_50_000) + 1; // +1 for coinbase
            Assert.Equal(expectedTxCount_21, lastBlock.Transactions.Count);

            // Ensure that all the transactions that were added is in the last block created.
            foreach (Transaction transaction in lastBlock.Transactions.Where(tx => !tx.IsCoinBase))
            {
                Assert.Contains(transaction.GetHash(), contractTransactionIds);
            }

            // Mine the remaining 5 transactions (tx #21 to #25)
            this.mockChain.MineBlocks(1);

            int restOfTx = txCount_25 - Convert.ToInt32(txGasPerBlockLimit_1_000_000 / gasLimit_50_000) + 1; // +1 for coinbase
            lastBlock = this.node1.GetLastBlock();
            Assert.Equal(restOfTx, lastBlock.Transactions.Count);
        }
    }

    public class PoAContractExecutionFailureTests : ContractExecutionFailureTests<PoAMockChainFixture>
    {
        public PoAContractExecutionFailureTests(PoAMockChainFixture fixture) : base(fixture)
        {
        }
    }

    public class PoWContractExecutionFailureTests : ContractExecutionFailureTests<PoWMockChainFixture>
    {
        public PoWContractExecutionFailureTests(PoWMockChainFixture fixture) : base(fixture)
        {
        }
    }
}
