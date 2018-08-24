﻿using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.Core.Receipts;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Receipts
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

            var noLogReceipt = new Receipt(new uint256(1234), 12345, new Log[] { log1, log2 });

            TestSerializeReceipt(noLogReceipt);
        }

        private void TestSerializeReceipt(Receipt receipt)
        {
            byte[] serialized = receipt.ToBytesRlp();
            Receipt deserialized = Receipt.FromBytesRlp(serialized);
            Assert.Equal(receipt.PostState, deserialized.PostState);
            Assert.Equal(receipt.GasUsed, deserialized.GasUsed);
            Assert.Equal(receipt.Bloom, deserialized.Bloom);
            Assert.Equal(receipt.Logs.Length, deserialized.Logs.Length);
            for(int i=0; i < receipt.Logs.Length; i++)
            {
                TestLogsEqual(receipt.Logs[i], deserialized.Logs[i]);
            }
        }

        private void TestLogsEqual(Log log1, Log log2)
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
