using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.CLR.ContractLogging;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class ContractLogHolderTests
    {
        private readonly ContractLogHolder logHolder;
        private readonly Network network;
        private readonly IContractPrimitiveSerializer serializer;

        public ContractLogHolderTests()
        {
            this.network = new SmartContractsRegTest();
            this.serializer = new ContractPrimitiveSerializer(this.network);
            this.logHolder = new ContractLogHolder();
        }

        [Fact]
        public void Store_Structs_And_Get_Logs()
        {
            var contractAddress1 = new uint160(1);
            var contractAddress2 = new uint160(2);

            var state1 = new TestSmartContractState(null, new TestMessage { ContractAddress = contractAddress1.ToAddress() }, null, null, null, null, null, null, null);
            var log1 = new Example1("Jordan", 12345);
            var log2 = new Example1("John", 123);

            var state2 = new TestSmartContractState(null, new TestMessage { ContractAddress = contractAddress2.ToAddress() }, null, null, null, null, null, null, null);
            var log3 = new Example2("0x95D34980095380851902ccd9A1Fb4C813C2cb639".HexToAddress(), 16, "This is a test message.");

            this.logHolder.Log(state1, log1);
            this.logHolder.Log(state1, log2);
            this.logHolder.Log(state2, log3);

            IList<Log> logs = this.logHolder.GetRawLogs().ToLogs(this.serializer);
            Assert.Equal(3, logs.Count);

            // First log has 3 topics, for name and 2 fields.
            Assert.Equal(contractAddress1, logs[0].Address);
            Assert.Equal(3, logs[0].Topics.Count);
            Assert.Equal((string) nameof(Example1), (string) Encoding.UTF8.GetString(logs[0].Topics[0]));
            Assert.Equal(log1.Name, (string) Encoding.UTF8.GetString(logs[0].Topics[1]));
            Assert.Equal(log1.Amount, BitConverter.ToUInt32(logs[0].Topics[2]));

            // Second log has 3 topics, for name and 2 fields.
            Assert.Equal(contractAddress1, logs[1].Address);
            Assert.Equal(3, logs[1].Topics.Count);
            Assert.Equal((string) nameof(Example1), (string) Encoding.UTF8.GetString(logs[1].Topics[0]));
            Assert.Equal(log2.Name, (string) Encoding.UTF8.GetString(logs[1].Topics[1]));
            Assert.Equal(log2.Amount, BitConverter.ToUInt32(logs[1].Topics[2]));

            // Third log has 3 topics, for name and 2 indexed fields - message is not included.
            Assert.Equal(contractAddress2, logs[2].Address);
            Assert.Equal(3, logs[2].Topics.Count);
            Assert.Equal((string) nameof(Example2), (string) Encoding.UTF8.GetString(logs[2].Topics[0]));
            Assert.Equal(log3.Address, new uint160(logs[2].Topics[1]).ToAddress());
            Assert.Equal(log3.Id, BitConverter.ToInt32(logs[2].Topics[2]));
        }

        public struct Example1
        {
            [Index]
            public string Name;
            [Index]
            public uint Amount;

            public Example1(string name, uint amount)
            {
                this.Name = name;
                this.Amount = amount;
            }
        }

        public struct Example2
        {
            [Index]
            public Address Address;
            [Index]
            public int Id;
            public string Message;

            public Example2(Address address, int id, string message)
            {
                this.Address = address;
                this.Id = id;
                this.Message = message;
            }
        }
    }
}
