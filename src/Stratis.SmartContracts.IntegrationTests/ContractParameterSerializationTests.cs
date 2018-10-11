using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Consensus;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Stratis.SmartContracts.IntegrationTests.MockChain;
using Xunit;
using Block = NBitcoin.Block;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class ContractParameterSerializationTests : IClassFixture<MockChainFixture>
    {
        private readonly Chain mockChain;
        private readonly Node node1;
        private readonly Node node2;

        private readonly IContractPrimitiveSerializer serializer; // TODO: May need to use a different serializer after serialization PR

        public ContractParameterSerializationTests(MockChainFixture fixture)
        {
            this.mockChain = fixture.Chain;
            this.node1 = this.mockChain.Nodes[0];
            this.node2 = this.mockChain.Nodes[1];
            this.serializer = new ContractPrimitiveSerializer(this.mockChain.Network);
        }

        [Fact]
        public void CreateContract_OneOfEachParameterType()
        {
            // Ensure fixture is funded.
            this.node1.MineBlocks(1);

            double amount = 25;
            uint256 currentHash = this.node1.GetLastBlock().GetHash();

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/CreateWithAllParameters.cs");
            Assert.True(compilationResult.Success);

            const char testChar = 'c';
            Address testAddress = new Address("mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn");
            const bool testBool = true;
            const int testInt = Int32.MaxValue;
            const long testLong = Int64.MaxValue;
            const uint testUint = UInt32.MaxValue;
            const ulong testUlong = UInt64.MaxValue;
            const string testString = "The quick brown fox jumps over the lazy dog";
            byte[] testBytes = new byte[] {0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09};

            string[] parameters = new string[]
            {
                string.Format("{0}#{1}", (int)MethodParameterDataType.Char, testChar), 
                string.Format("{0}#{1}", (int)MethodParameterDataType.Address, testAddress), 
                string.Format("{0}#{1}", (int)MethodParameterDataType.Bool, testBool),
                string.Format("{0}#{1}", (int)MethodParameterDataType.Int, testInt),
                string.Format("{0}#{1}", (int)MethodParameterDataType.Long, testLong),
                string.Format("{0}#{1}", (int)MethodParameterDataType.UInt, testUint),
                string.Format("{0}#{1}", (int)MethodParameterDataType.ULong, testUlong),
                string.Format("{0}#{1}", (int)MethodParameterDataType.String, testString),
                string.Format("{0}#{1}", (int)MethodParameterDataType.ByteArray, testBytes.ToHexString()),
            };
            BuildCreateContractTransactionResponse response = this.node1.SendCreateContractTransaction(compilationResult.Compilation, amount, parameters);
            this.node2.WaitMempoolCount(1);
            this.node2.MineBlocks(1);
            Block lastBlock = this.node1.GetLastBlock();

            // Blocks progressed
            Assert.NotEqual(currentHash, lastBlock.GetHash());

            // Contract was created
            Assert.NotNull(this.node1.GetCode(response.NewContractAddress));

            // Block doesn't contain any extra transactions
            Assert.Equal(2, lastBlock.Transactions.Count);

            // Contract keeps balance
            Assert.Equal((ulong)new Money((ulong)amount, MoneyUnit.BTC), this.node1.GetContractBalance(response.NewContractAddress));

            // All values were stored
            Assert.Equal(this.serializer.Serialize(testChar), this.node1.GetStorageValue(response.NewContractAddress, "char"));
            Assert.Equal(this.serializer.Serialize(testAddress), this.node1.GetStorageValue(response.NewContractAddress, "Address"));
            Assert.Equal(this.serializer.Serialize(testBool), this.node1.GetStorageValue(response.NewContractAddress, "bool"));
            Assert.Equal(this.serializer.Serialize(testInt), this.node1.GetStorageValue(response.NewContractAddress, "int"));
            Assert.Equal(this.serializer.Serialize(testLong), this.node1.GetStorageValue(response.NewContractAddress, "long"));
            Assert.Equal(this.serializer.Serialize(testUint), this.node1.GetStorageValue(response.NewContractAddress, "uint"));
            Assert.Equal(this.serializer.Serialize(testUlong), this.node1.GetStorageValue(response.NewContractAddress, "ulong"));
            Assert.Equal(this.serializer.Serialize(testString), this.node1.GetStorageValue(response.NewContractAddress, "string"));
            Assert.Equal(testBytes, this.node1.GetStorageValue(response.NewContractAddress, "bytes"));

            // Test that the contract address, event name, and logging values are available in the bloom.
            var scBlockHeader = lastBlock.Header as SmartContractBlockHeader;
            Assert.True(scBlockHeader.LogsBloom.Test(new Address(response.NewContractAddress).ToUint160(this.mockChain.Network).ToBytes()));
            Assert.True(scBlockHeader.LogsBloom.Test(Encoding.UTF8.GetBytes("Log")));
            Assert.True(scBlockHeader.LogsBloom.Test(this.serializer.Serialize(testChar)));
            Assert.True(scBlockHeader.LogsBloom.Test(this.serializer.Serialize(testAddress)));
            Assert.True(scBlockHeader.LogsBloom.Test(this.serializer.Serialize(testBool)));
            Assert.True(scBlockHeader.LogsBloom.Test(this.serializer.Serialize(testInt)));
            Assert.True(scBlockHeader.LogsBloom.Test(this.serializer.Serialize(testLong)));
            Assert.True(scBlockHeader.LogsBloom.Test(this.serializer.Serialize(testUint)));
            Assert.True(scBlockHeader.LogsBloom.Test(this.serializer.Serialize(testUlong)));
            Assert.True(scBlockHeader.LogsBloom.Test(this.serializer.Serialize(testString)));
            Assert.True(scBlockHeader.LogsBloom.Test(this.serializer.Serialize(testBytes)));
            // And sanity test that random fields aren't contained in bloom.
            Assert.False(scBlockHeader.LogsBloom.Test(Encoding.UTF8.GetBytes("RandomValue")));
            Assert.False(scBlockHeader.LogsBloom.Test(BitConverter.GetBytes(123)));

            // Receipt is correct
            ReceiptResponse receipt = this.node1.GetReceipt(response.TransactionId.ToString());
            Assert.Equal(lastBlock.GetHash().ToString(), receipt.BlockHash);
            Assert.Equal(response.TransactionId.ToString(), receipt.TransactionHash);
            Assert.True(receipt.Success);
            Assert.Single(receipt.Logs);
            Assert.True(receipt.GasUsed > GasPriceList.BaseCost);
            Assert.Equal(response.NewContractAddress, receipt.NewContractAddress);
            Assert.Equal(this.node1.MinerAddress.Address, receipt.From);
            Assert.Null(receipt.To);
            Assert.Null(receipt.Error);
        }
    }
}
