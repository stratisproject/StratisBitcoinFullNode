using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Serialization;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class PersistentStateSerializerTests
    {
        private PersistentStateSerializer serializer;

        public PersistentStateSerializerTests()
        {
            this.serializer = new PersistentStateSerializer();
        }

        [Fact]
        public void PersistentState_CanSerializeAllTypes()
        {
            // Checking that these all work for now. 
            // TODO: Check that these actually are serialized in a performant way
            TestType<Address>(new uint160(123456).ToAddress(Network.SmartContractsRegTest));
            TestType<bool>(true);
            TestType<int>((int)32);
            TestType<long>((long)6775492);
            TestType<uint>((uint)101);
            TestType<ulong>((ulong)1245);
            TestType<byte>(new byte());
            TestType<sbyte>(new sbyte());
            TestType<byte[]>(new byte[] { 127, 123 });
            TestType<char>('c');
            TestType<string>("Test String");
        }

        [Fact]
        public void PersistentState_CanSerialize_Deserialize_ValueType()
        {
            var network = Network.SmartContractsRegTest;

            TestValueType valueType = this.NewTestValueType();

            var serialized = this.serializer.Serialize(valueType, network);

            var deserialized = this.serializer.Deserialize<TestValueType>(serialized, network);

            Assert.Equal(valueType.AddressField, deserialized.AddressField);
            Assert.Equal(valueType.AddressProp, deserialized.AddressProp);
            Assert.Equal(valueType.BoolField, deserialized.BoolField);
            Assert.Equal(valueType.BoolProp, deserialized.BoolProp);
            Assert.Equal(valueType.IntField, deserialized.IntField);
            Assert.Equal(valueType.IntProp, deserialized.IntProp);
            Assert.Equal(valueType.LongField, deserialized.LongField);
            Assert.Equal(valueType.LongProp, deserialized.LongProp);
            Assert.Equal(valueType.UintField, deserialized.UintField);
            Assert.Equal(valueType.UintProp, deserialized.UintProp);
            Assert.Equal(valueType.UlongField, deserialized.UlongField);
            Assert.Equal(valueType.UlongProp, deserialized.UlongProp);
            Assert.Equal(valueType.ByteField, deserialized.ByteField);
            Assert.Equal(valueType.ByteProp, deserialized.ByteProp);        
            Assert.Equal(valueType.SbyteField, deserialized.SbyteField);
            Assert.Equal(valueType.SbyteProp, deserialized.SbyteProp);        
            Assert.Equal(valueType.ByteArrayField, deserialized.ByteArrayField);
            Assert.Equal(valueType.ByteArrayProp, deserialized.ByteArrayProp);
            Assert.Equal(valueType.CharField, deserialized.CharField);
            Assert.Equal(valueType.CharProp, deserialized.CharProp);
            Assert.Equal(valueType.StringField, deserialized.StringField);
            Assert.Equal(valueType.StringProp, deserialized.StringProp);
        }

        [Fact]
        public void PersistentState_CanSerialize_Deserialize_NestedValueType()
        {
            var network = Network.SmartContractsRegTest;

            TestValueType valueType = this.NewTestValueType();

            NestedValueType nestedValueType = new NestedValueType();
            nestedValueType.Id = 123;
            nestedValueType.ValueType = valueType;

            var serialized = this.serializer.Serialize(nestedValueType, network);

            var deserialized = this.serializer.Deserialize<NestedValueType>(serialized, network);

            var nested = deserialized.ValueType;

            Assert.Equal(nestedValueType.Id, deserialized.Id);
            Assert.Equal(valueType.AddressField, nested.AddressField);
            Assert.Equal(valueType.AddressProp, nested.AddressProp);
            Assert.Equal(valueType.BoolField, nested.BoolField);
            Assert.Equal(valueType.BoolProp, nested.BoolProp);
            Assert.Equal(valueType.IntField, nested.IntField);
            Assert.Equal(valueType.IntProp, nested.IntProp);
            Assert.Equal(valueType.LongField, nested.LongField);
            Assert.Equal(valueType.LongProp, nested.LongProp);
            Assert.Equal(valueType.UintField, nested.UintField);
            Assert.Equal(valueType.UintProp, nested.UintProp);
            Assert.Equal(valueType.UlongField, nested.UlongField);
            Assert.Equal(valueType.UlongProp, nested.UlongProp);
            Assert.Equal(valueType.ByteField, nested.ByteField);
            Assert.Equal(valueType.ByteProp, nested.ByteProp);
            Assert.Equal(valueType.SbyteField, nested.SbyteField);
            Assert.Equal(valueType.SbyteProp, nested.SbyteProp);
            Assert.Equal(valueType.ByteArrayField, nested.ByteArrayField);
            Assert.Equal(valueType.ByteArrayProp, nested.ByteArrayProp);
            Assert.Equal(valueType.CharField, nested.CharField);
            Assert.Equal(valueType.CharProp, nested.CharProp);
            Assert.Equal(valueType.StringField, nested.StringField);
            Assert.Equal(valueType.StringProp, nested.StringProp);
        }

        [Fact]
        public void PersistentState_CanSerialize_Deserialize_NullValueType()
        {
            var network = Network.SmartContractsRegTest;

            var nestedValueType = new HasReferenceTypeValueType();
            nestedValueType.ReferenceType = null;

            var serialized = this.serializer.Serialize(nestedValueType, network);

            var deserialized = this.serializer.Deserialize<HasReferenceTypeValueType>(serialized, network);

            Assert.Equal(nestedValueType.ReferenceType, deserialized.ReferenceType);
        }

        private TestValueType NewTestValueType()
        {
            var instance = new TestValueType();
            instance.AddressField = new Address("abc123");
            instance.AddressProp = new Address("123abc");
            instance.BoolField = true;
            instance.BoolProp = true;
            instance.IntField = 123;
            instance.IntProp = 456;
            instance.LongField = 123L;
            instance.LongProp = 456L;
            instance.UintField = 123u;
            instance.UintProp = 456u;
            instance.UlongField = 123ul;
            instance.UlongProp = 456ul;
            instance.ByteField = 0x16;
            instance.ByteProp = 0x61;
            instance.SbyteField = sbyte.MinValue;
            instance.SbyteProp = sbyte.MaxValue;
            instance.ByteArrayField = new byte[] { 0x12, 0x24, 0x36, 0x48 };
            instance.ByteArrayProp = new byte[] { 0x12, 0x24, 0x36, 0x48 };
            instance.CharField = 'a';
            instance.CharProp = 'b';
            instance.StringField = "Test123";
            instance.StringProp = "123Test";

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
        public Address AddressProp { get; set; }

        public bool BoolField;
        public bool BoolProp { get; set; }

        public int IntField;
        public int IntProp { get; set; }

        public long LongField;
        public long LongProp { get; set; }

        public uint UintField;
        public uint UintProp { get; set; }

        public ulong UlongField;
        public ulong UlongProp { get; set; }

        public byte ByteField;
        public byte ByteProp { get; set; }

        public sbyte SbyteField;
        public sbyte SbyteProp { get; set; }

        public byte[] ByteArrayField;
        public byte[] ByteArrayProp { get; set; }

        public char CharField;
        public char CharProp { get; set; }

        public string StringField;
        public string StringProp { get; set; }
    }
}