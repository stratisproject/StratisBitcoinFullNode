using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.Core.Receipts;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests.Receipts
{
    public class ReceiptSerializationTest
    {
        [Fact]
        public void Log_Serializes_And_Deserializes()
        {
            var data = new byte[] { 1, 2, 3, 4 };
            var topics = new List<byte[]>
            {
                Encoding.UTF8.GetBytes("ALogStructName"),
                BitConverter.GetBytes(123)
            };
            var log = new Log(new uint160(1234), topics, data);

            byte[] serialized = log.ToBytesRlp();
            Log deserialized = Log.FromBytesRlp(serialized);
            TestLogsEqual(log, deserialized);
        }

        [Fact]
        public void Receipt_Serializes_And_Deserializes()
        {
            var data1 = new byte[] { 1, 2, 3, 4 };
            var topics1 = new List<byte[]>
            {
                Encoding.UTF8.GetBytes("ALogStructName"),
                BitConverter.GetBytes(123)
            };
            var log1 = new Log(new uint160(1234), topics1, data1);

            var data2 = new byte[] { 4, 5, 6, 7 };
            var topics2 = new List<byte[]>
            {
                Encoding.UTF8.GetBytes("ALogStructName2"),
                BitConverter.GetBytes(234)
            };
            var log2 = new Log(new uint160(12345), topics2, data2);

            var receipt = new Receipt(new uint256(1234), 12345, new Log[] { log1, log2 });
            TestConsensusSerialize(receipt);
            receipt = new Receipt(receipt.PostState, receipt.GasUsed, receipt.Logs, new uint256(12345), new uint160(25), new uint160(24), new uint160(23), true, null, null) { BlockHash = new uint256(1234) };
            TestStorageSerialize(receipt);

            // Test cases where either the sender or contract is null - AKA CALL vs CREATE
            receipt = new Receipt(receipt.PostState, receipt.GasUsed, receipt.Logs, new uint256(12345), new uint160(25), new uint160(24), null, true, "Test Result", "Test Error Message") { BlockHash = new uint256(1234) };
            TestStorageSerialize(receipt);
            receipt = new Receipt(receipt.PostState, receipt.GasUsed, receipt.Logs, new uint256(12345), new uint160(25), null, new uint160(23), true, "Test Result 2", "Test Error Message 2") { BlockHash = new uint256(1234) };
            TestStorageSerialize(receipt);
        }

        private void TestConsensusSerialize(Receipt receipt)
        {
            byte[] serialized = receipt.ToConsensusBytesRlp();
            Receipt deserialized = Receipt.FromConsensusBytesRlp(serialized);
            Assert.Equal(receipt.PostState, deserialized.PostState);
            Assert.Equal(receipt.GasUsed, deserialized.GasUsed);
            Assert.Equal(receipt.Bloom, deserialized.Bloom);
            Assert.Equal(receipt.Logs.Length, deserialized.Logs.Length);

            for(int i=0; i < receipt.Logs.Length; i++)
            {
                TestLogsEqual(receipt.Logs[i], deserialized.Logs[i]);
            }
        }

        private void TestStorageSerialize(Receipt receipt)
        {
            byte[] serialized = receipt.ToStorageBytesRlp();
            Receipt deserialized = Receipt.FromStorageBytesRlp(serialized);
            TestStorageReceiptEquality(receipt, deserialized);
        }

        /// <summary>
        /// Ensures 2 receipts and all their properties are equal.
        /// </summary>
        public static void TestStorageReceiptEquality(Receipt receipt1, Receipt receipt2)
        {
            Assert.Equal(receipt1.PostState, receipt2.PostState);
            Assert.Equal(receipt1.GasUsed, receipt2.GasUsed);
            Assert.Equal(receipt1.Bloom, receipt2.Bloom);
            Assert.Equal(receipt1.Logs.Length, receipt2.Logs.Length);

            for (int i = 0; i < receipt1.Logs.Length; i++)
            {
                TestLogsEqual(receipt1.Logs[i], receipt2.Logs[i]);
            }

            Assert.Equal(receipt1.TransactionHash, receipt2.TransactionHash);
            Assert.Equal(receipt1.BlockHash, receipt2.BlockHash);
            Assert.Equal(receipt1.From, receipt2.From);
            Assert.Equal(receipt1.To, receipt2.To);
            Assert.Equal(receipt1.NewContractAddress, receipt2.NewContractAddress);
            Assert.Equal(receipt1.Success, receipt2.Success);
            Assert.Equal(receipt1.ErrorMessage, receipt2.ErrorMessage);
        }

        private static void TestLogsEqual(Log log1, Log log2)
        {
            Assert.Equal(log1.Address, log2.Address);
            Assert.Equal(log1.Data, log2.Data);
            Assert.Equal(log1.Topics.Count, log2.Topics.Count);
            for(int i=0; i < log1.Topics.Count; i++)
            {
                Assert.Equal(log1.Topics[i], log2.Topics[i]);
            }
        }
    }
}
