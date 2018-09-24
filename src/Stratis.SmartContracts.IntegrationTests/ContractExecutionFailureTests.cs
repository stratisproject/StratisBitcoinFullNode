﻿using System;
using CSharpFunctionalExtensions;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.IntegrationTests.MockChain;
using Xunit;
using Block = NBitcoin.Block;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class ContractExecutionFailureTests : IClassFixture<MockChainFixture>
    {
        private readonly Chain mockChain;
        private readonly Node node1;
        private readonly Node node2;

        private readonly ISenderRetriever senderRetriever;

        public ContractExecutionFailureTests(MockChainFixture fixture)
        {
            this.mockChain = fixture.Chain;
            this.node1 = this.mockChain.Nodes[0];
            this.node2 = this.mockChain.Nodes[1];
            this.senderRetriever = new SenderRetriever();
        }

        // TODO: The costs definitely need to be refined! Contract execution shouldn't be so cheap relative to fees.

        // Also check that validation and base cost fees are being applied correctly.

        [Fact]
        public void ContractTransaction_InvalidSerialization()
        {
            // Create poorly serialized transaction
            var random = new Random();
            byte[] bytes = new byte[101];
            random.NextBytes(bytes);
            bytes[0] = (byte) ScOpcodeType.OP_CALLCONTRACT;

            // Send fails - doesn't even make it to mempool
            Result<WalletSendTransactionModel> result = this.node1.SendTransaction(new Script(bytes), 25);
            Assert.True(result.IsFailure);
            Assert.Equal("Invalid ContractTxData format", result.Error); // TODO: const error message
        }

        [Fact]
        public void ContractTransaction_InvalidByteCode()
        {
            // Ensure fixture is funded.
            this.node1.MineBlocks(1);

            double amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            // Create transaction with random bytecode.
            var random = new Random();
            byte[] bytes = new byte[100];
            random.NextBytes(bytes);
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(bytes, amount);
            this.node2.WaitMempoolCount(1);
            this.node2.MineBlocks(1);
            Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Contract wasn't created
            Assert.Null(this.node1.GetCode(response.NewContractAddress));

            // Block contains a refund transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction refundTransaction = lastBlock.Transactions[2];
            uint160 refundReceiver = this.senderRetriever.GetAddressFromScript(refundTransaction.Outputs[0].ScriptPubKey).Sender;
            Assert.Equal(this.node1.MinerAddress.Address, refundReceiver.ToAddress(this.mockChain.Network).Value);
            Assert.Equal(new Money((long) amount, MoneyUnit.BTC), refundTransaction.Outputs[0].Value);
            Money fee = lastBlock.Transactions[0].Outputs[0].Value - new Money(50, MoneyUnit.BTC);

            // Amount was refunded to wallet, minus fee
            Assert.Equal(senderBalanceBefore - this.node1.WalletSpendableBalance, fee);

            // Receipt looks as expected.
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs);
            Assert.False(receipt.Success);
            Assert.Equal(GasPriceList.BaseCost, receipt.GasUsed);
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);

        }

        [Fact]
        public void ContractTransaction_ValidationFailure()
        {
            // Ensure fixture is funded.
            this.node1.MineBlocks(1);

            double amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            // Create transaction with non-deterministic bytecode.
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/NonDeterministicContract.cs");
            Assert.True(compilationResult.Success);

            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount);
            this.node2.WaitMempoolCount(1);
            this.node2.MineBlocks(1);
            Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Contract wasn't created
            Assert.Null(this.node2.GetCode(response.NewContractAddress));

            // Block contains a refund transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction refundTransaction = lastBlock.Transactions[2];
            uint160 refundReceiver = this.senderRetriever.GetAddressFromScript(refundTransaction.Outputs[0].ScriptPubKey).Sender;
            Assert.Equal(this.node1.MinerAddress.Address, refundReceiver.ToAddress(this.mockChain.Network).Value);
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), refundTransaction.Outputs[0].Value);
            Money fee = lastBlock.Transactions[0].Outputs[0].Value - new Money(50, MoneyUnit.BTC);

            // Amount was refunded to wallet, minus fee
            Assert.Equal(senderBalanceBefore - this.node1.WalletSpendableBalance, fee);

            // Receipt looks as expected.
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs);
            Assert.False(receipt.Success);
            Assert.Equal(GasPriceList.BaseCost, receipt.GasUsed);
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
        }

        [Fact]
        public void ContractTransaction_ExceptionInCreate()
        {
            // Ensure fixture is funded.
            this.node1.MineBlocks(1);

            double amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ExceptionInConstructor.cs");
            Assert.True(compilationResult.Success);

            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount);
            this.node2.WaitMempoolCount(1);
            this.node2.MineBlocks(1);
            Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Contract wasn't created
            Assert.Null(this.node2.GetCode(response.NewContractAddress));

            // Block contains a refund transaction
            Assert.Equal(3, lastBlock.Transactions.Count);
            Transaction refundTransaction = lastBlock.Transactions[2];
            uint160 refundReceiver = this.senderRetriever.GetAddressFromScript(refundTransaction.Outputs[0].ScriptPubKey).Sender;
            Assert.Equal(this.node1.MinerAddress.Address, refundReceiver.ToAddress(this.mockChain.Network).Value);
            Assert.Equal(new Money((long)amount, MoneyUnit.BTC), refundTransaction.Outputs[0].Value);
            Money fee = lastBlock.Transactions[0].Outputs[0].Value - new Money(50, MoneyUnit.BTC);

            // Amount was refunded to wallet, minus fee
            Assert.Equal(senderBalanceBefore - this.node1.WalletSpendableBalance, fee);

            // Receipt looks correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.Empty(receipt.Logs);
            Assert.False(receipt.Success);
            Assert.True(receipt.GasUsed > GasPriceList.BaseCost);
            Assert.Null(receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.StartsWith("System.IndexOutOfRangeException", receipt.Error);
        }

        [Fact(Skip ="TODO")]
        public void ContractTransaction_ExceptionInCall()
        {
            // Exception thrown inside call - no prior logged events or storage deployed. Funds sent back. 
        }

        [Fact(Skip = "TODO")]
        public void ContractTransaction_AddressDoesntExist()
        {
            // Contract address doesn't exist
        }

        [Fact(Skip = "TODO")]
        public void ContractTransaction_MethodDoesntExist()
        {
            // Contract method doesn't exist
        }
    }
}
