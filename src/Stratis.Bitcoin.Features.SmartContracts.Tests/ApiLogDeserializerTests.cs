using System;
using System.Linq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class ApiLogDeserializerTests
    {
        public struct TestLog
        {
            public uint Id;
            public string Name;
            public byte Data;
            public byte[] Datas;
            public bool Truth;
            public Address Address;
        }

        [Fact]
        public void Deserialize_Basic_Log_Success()
        {
            var network = new SmartContractsRegTest();
            var primitiveSerializer = new ContractPrimitiveSerializer(network);

            var testStruct = new TestLog
            {
                Id = uint.MaxValue,
                Name = "Test ID",
                Data = 0xAA,
                Datas = new byte[] { 0xBB, 0xCC, 0xDD },
                Truth = true,
                Address = "0x0000000000000000000000000000000000000001".HexToAddress()
            };

            var testBytes = primitiveSerializer.Serialize(testStruct);

            var serializer = new ApiLogDeserializer(primitiveSerializer, network);
            dynamic deserializedLog = serializer.DeserializeLogData(testBytes, typeof(TestLog));

            Assert.Equal(testStruct.Id, deserializedLog.Id);
            Assert.Equal(testStruct.Name, deserializedLog.Name);
            Assert.Equal(testStruct.Data, deserializedLog.Data);
            Assert.True(testStruct.Datas.SequenceEqual((byte[])deserializedLog.Datas));
            Assert.Equal(testStruct.Truth, deserializedLog.Truth);
            Assert.Equal(testStruct.Address.ToUint160().ToBase58Address(network), deserializedLog.Address);
        }
    }
}
