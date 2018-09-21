using System;
using CSharpFunctionalExtensions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.IntegrationTests.MockChain;
using Xunit;
namespace Stratis.SmartContracts.IntegrationTests
{
    public class ContractExecutionFailureTests : IClassFixture<MockChainFixture>
    {
        private readonly Chain mockChain;
        private readonly Node sender;
        private readonly Node receiver;

        public ContractExecutionFailureTests(MockChainFixture fixture)
        {
            this.mockChain = fixture.Chain;
            this.sender = this.mockChain.Nodes[0];
            this.receiver = this.mockChain.Nodes[1];
        }

        [Fact]
        public void ContractTransaction_InvalidSerialization()
        {
            // Create poorly serialized transaction
            var random = new Random();
            byte[] bytes = new byte[101];
            random.NextBytes(bytes);
            bytes[0] = (byte) ScOpcodeType.OP_CALLCONTRACT;

            // Send fails
            Result<WalletSendTransactionModel> result = this.sender.SendTransaction(new Script(bytes), 25);
            Assert.True(result.IsFailure);
            Assert.Equal("Invalid ContractTxData format", result.Error); // TODO: const error message
        }

        [Fact]
        public void ContractTransaction_InvalidByteCode()
        {
            // Nonsensical bytecode - included in block but refund given, no contract deployed.

        }

        [Fact]
        public void ContractTransaction_NonDeterministicByteCode()
        {
            // Nondeterministic bytecode - included in block but refund given, no contract deployed.
        }

        [Fact]
        public void ContractTransaction_ExceptionInCreate()
        {
            // Exception thrown inside create - contract not deployed. No prior logged events or storage deployed. Funds sent back.
        }

        [Fact]
        public void ContractTransaction_ExceptionInCall()
        {
            // Exception thrown inside call - no prior logged events or storage deployed. Funds sent back. 
        }
        
        [Fact]
        public void ContractTransaction_AddressDoesntExist()
        {
            // Contract address doesn't exist
        }

        [Fact]
        public void ContractTransaction_MethodDoesntExist()
        {
            // Contract method doesn't exist
        }

        ~ContractExecutionFailureTests()
        {
            this.mockChain?.Dispose();
        }
    }
}
