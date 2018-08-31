using System.Text;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class PersistentStateSerializerTests
    {
        private readonly ContractPrimitiveSerializer serializer;
        private readonly Network network;

        public PersistentStateSerializerTests()
        {
            this.network = new SmartContractsRegTest();
            this.serializer = new ContractPrimitiveSerializer(this.network);
        }

        [Fact]
        public void PersistentState_CanSerializeAllTypes()
        {
            TestType<Address>(new uint160(123456).ToAddress(this.network));
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
            TestValueType valueType = this.NewTestValueType();

            var serialized = this.serializer.Serialize(valueType);

            TestValueType deserialized = this.serializer.Deserialize<TestValueType>(serialized);
            TestValueTypeEqual(valueType, deserialized);
        }

        [Fact]
        public void PersistentState_CanSerialize_Deserialize_NestedValueType()
        {
            TestValueType valueType = this.NewTestValueType();

            NestedValueType nestedValueType = new NestedValueType();
            nestedValueType.Id = 123;
            nestedValueType.ValueType = valueType;

            var serialized = this.serializer.Serialize(nestedValueType);

            NestedValueType deserialized = this.serializer.Deserialize<NestedValueType>(serialized);

            TestValueType nested = deserialized.ValueType;

            Assert.Equal(nestedValueType.Id, deserialized.Id);
            TestValueTypeEqual(valueType, nested);
        }

        [Fact]
        public void PersistentState_CanSerialize_Deserialize_NullValueType()
        {
            var nestedValueType = new HasReferenceTypeValueType();
            nestedValueType.ReferenceType = null;

            var serialized = this.serializer.Serialize(nestedValueType);

            HasReferenceTypeValueType deserialized = this.serializer.Deserialize<HasReferenceTypeValueType>(serialized);

            Assert.Equal(nestedValueType.ReferenceType, deserialized.ReferenceType);
        }

        [Fact]
        public void PersistentState_NonValueType_Fails()
        {
            var classToSave = new ReferencedType();
            classToSave.TestValueType = NewTestValueType();

            Assert.Throws<ContractPrimitiveSerializationException>(() =>
                this.serializer.Serialize(classToSave));
        }

        [Fact]
        public void PersistentState_NestedType_Success()
        {
            var complexType = new ComplexValueType();
            complexType.Id = 678;
            complexType.String = "TestString";
            complexType.NestedValueType = new NestedValueType();
            complexType.NestedValueType.ValueType = NewTestValueType();
            complexType.TestValueType = NewTestValueType();

            byte[] serialized = this.serializer.Serialize(complexType);
            ComplexValueType deserialized = this.serializer.Deserialize<ComplexValueType>(serialized);
            Assert.Equal(complexType.Id, deserialized.Id);
            Assert.Equal(complexType.String, deserialized.String);
            Assert.Equal(complexType.NestedValueType.Id, deserialized.NestedValueType.Id);
            TestValueTypeEqual(complexType.NestedValueType.ValueType, complexType.NestedValueType.ValueType);
            TestValueTypeEqual(complexType.TestValueType, deserialized.TestValueType);

            // Lets show improvement on json for fun
            byte[] jsonSerialized = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(complexType));
            Assert.True(serialized.Length < jsonSerialized.Length);
        }

        private TestValueType NewTestValueType()
        {
            var instance = new TestValueType();
            instance.AddressField = new uint160(123456).ToAddress(this.network);
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
            byte[] testBytes = this.serializer.Serialize(input);
            T output = this.serializer.Deserialize<T>(testBytes);
            Assert.Equal(input, output);
        }

        private void TestValueTypeEqual(TestValueType type1, TestValueType type2)
        {
            Assert.Equal(type1.AddressField, type2.AddressField);
            Assert.Equal(type1.BoolField, type2.BoolField);
            Assert.Equal(type1.IntField, type2.IntField);
            Assert.Equal(type1.LongField, type2.LongField);
            Assert.Equal(type1.UintField, type2.UintField);
            Assert.Equal(type1.UlongField, type2.UlongField);
            Assert.Equal(type1.ByteField, type2.ByteField);
            Assert.Equal(type1.ByteArrayField, type2.ByteArrayField);
            Assert.Equal(type1.CharField, type2.CharField);
            Assert.Equal(type1.StringField, type2.StringField);
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

    public struct ComplexValueType
    {
        public TestValueType TestValueType;

        public NestedValueType NestedValueType;

        public int Id;

        public string String;
    }

    public class ReferencedType
    {
        public TestValueType TestValueType;
    }
}