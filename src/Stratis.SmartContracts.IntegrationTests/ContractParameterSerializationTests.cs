using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Stratis.SmartContracts.IntegrationTests.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class ContractParameterSerializationTests : IClassFixture<MockChainFixture>
    {
        private readonly Chain mockChain;
        private readonly Node node1;
        private readonly Node node2;

        public ContractParameterSerializationTests(MockChainFixture fixture)
        {
            this.mockChain = fixture.Chain;
            this.node1 = this.mockChain.Nodes[0];
            this.node2 = this.mockChain.Nodes[1];
        }

        [Fact]
        public void CreateContract_OneOfEachParameterType()
        {
            // Ensure fixture is funded.
            this.node1.MineBlocks(1);

            double amount = 25;
            Money senderBalanceBefore = this.node1.WalletSpendableBalance;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/BasicParameters.cs");
            Assert.True(compilationResult.Success);
            string[] parameters = new string[]
            {
                string.Format("{0}#{1}", (int)MethodParameterDataType.Char, 'c'), // char
                string.Format("{0}#{1}", (int)MethodParameterDataType.Address, new Address("mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn")), // Address
                string.Format("{0}#{1}", (int)MethodParameterDataType.Bool, true), // bool
                string.Format("{0}#{1}", (int)MethodParameterDataType.Int, Int32.MaxValue), // int
                string.Format("{0}#{1}", (int)MethodParameterDataType.ULong, UInt64.MaxValue), // long
                string.Format("{0}#{1}", (int)MethodParameterDataType.ULong, UInt64.MaxValue), // uint
                string.Format("{0}#{1}", (int)MethodParameterDataType.ULong, UInt64.MaxValue), // ulong
                string.Format("{0}#{1}", (int)MethodParameterDataType.ULong, UInt64.MaxValue), // string
                string.Format("{0}#{1}", (int)MethodParameterDataType.ULong, UInt64.MaxValue), // byte[]
            };
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount, parameters);
            this.node2.WaitMempoolCount(1);
            this.node2.MineBlocks(1);
            Block lastBlock = this.node1.GetLastBlock();
        }
    }
}
