using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class PersistentStateSerializerTests
    {
        private readonly PersistentStateSerializer serializer;

        public PersistentStateSerializerTests()
        {
            this.serializer = new PersistentStateSerializer();
        }

        [Fact]
        public void PersistentState_CanSerializeAllTypes()
        {
            TestType<Address>(new uint160(123456).ToAddress(Network.SmartContractsRegTest));
            TestType<bool>(true);
            TestType<int>((int)32);
            TestType<long>((long)6775492);
            TestType<uint>((uint)101);
            TestType<ulong>((ulong)1245);
            TestType<byte>(new byte());
            TestType<byte[]>(new byte[] { 127, 123 });
            TestType<char>('c');
            TestType<string>("Test String");
        }

        [Fact]
        public void PersistentState_CanSerialize_Deserialize_ValueType()
        {
            Network network = Network.SmartContractsRegTest;

            TestValueType valueType = this.NewTestValueType();

            var serialized = this.serializer.Serialize(valueType, network);

            TestValueType deserialized = this.serializer.Deserialize<TestValueType>(serialized, network);

            Assert.Equal(valueType.AddressField, deserialized.AddressField);
            Assert.Equal(valueType.BoolField, deserialized.BoolField);
            Assert.Equal(valueType.IntField, deserialized.IntField);
            Assert.Equal(valueType.LongField, deserialized.LongField);
            Assert.Equal(valueType.UintField, deserialized.UintField);
            Assert.Equal(valueType.UlongField, deserialized.UlongField);
            Assert.Equal(valueType.ByteField, deserialized.ByteField);
            Assert.Equal(valueType.ByteArrayField, deserialized.ByteArrayField);
            Assert.Equal(valueType.CharField, deserialized.CharField);
            Assert.Equal(valueType.StringField, deserialized.StringField);
        }

        [Fact]
        public void PersistentState_CanSerialize_Deserialize_NestedValueType()
        {
            Network network = Network.SmartContractsRegTest;

            TestValueType valueType = this.NewTestValueType();

            NestedValueType nestedValueType = new NestedValueType();
            nestedValueType.Id = 123;
            nestedValueType.ValueType = valueType;

            var serialized = this.serializer.Serialize(nestedValueType, network);

            NestedValueType deserialized = this.serializer.Deserialize<NestedValueType>(serialized, network);

            TestValueType nested = deserialized.ValueType;

            Assert.Equal(nestedValueType.Id, deserialized.Id);
            Assert.Equal(valueType.AddressField, nested.AddressField);
            Assert.Equal(valueType.BoolField, nested.BoolField);
            Assert.Equal(valueType.IntField, nested.IntField);
            Assert.Equal(valueType.LongField, nested.LongField);
            Assert.Equal(valueType.UintField, nested.UintField);
            Assert.Equal(valueType.UlongField, nested.UlongField);
            Assert.Equal(valueType.ByteField, nested.ByteField);
            Assert.Equal(valueType.ByteArrayField, nested.ByteArrayField);
            Assert.Equal(valueType.CharField, nested.CharField);
            Assert.Equal(valueType.StringField, nested.StringField);
        }

        [Fact]
        public void PersistentState_CanSerialize_Deserialize_NullValueType()
        {
            Network network = Network.SmartContractsRegTest;

            var nestedValueType = new HasReferenceTypeValueType();
            nestedValueType.ReferenceType = null;

            var serialized = this.serializer.Serialize(nestedValueType, network);

            HasReferenceTypeValueType deserialized = this.serializer.Deserialize<HasReferenceTypeValueType>(serialized, network);

            Assert.Equal(nestedValueType.ReferenceType, deserialized.ReferenceType);
        }

        private TestValueType NewTestValueType()
        {
            var instance = new TestValueType();
            instance.AddressField = new uint160(123456).ToAddress(Network.SmartContractsRegTest);
            instance.BoolField = true;
            instance.IntField = 123;
            instance.LongField = 123L;
            instance.UintField = 123u;
            instance.UlongField = 123ul;
            instance.ByteField = 0x16;
            instance.ByteArrayField = new byte[] { 0x12, 0x24, 0x36, 0x48 };
            instance.CharField = 'a';
            instance.StringField = "Test123";

            return instance;
        }

        private void TestType<T>(T input)
        {
            byte[] testBytes = this.serializer.Serialize(input, Network.SmartContractsRegTest);
            T output = this.serializer.Deserialize<T>(testBytes, Network.SmartContractsRegTest);
            Assert.Equal(input, output);
        }
    }

    public struct HasReferenceTypeValueType
    {
        public string ReferenceType;
    }

    public struct NestedValueType
    {
        public int Id;
        public TestValueType ValueType;
    }

    public struct TestValueType
    {
        public Address AddressField;

        public bool BoolField;

        public int IntField;

        public long LongField;

        public uint UintField;

        public ulong UlongField;

        public byte ByteField;

        public byte[] ByteArrayField;

        public char CharField;

        public string StringField;
    }
}