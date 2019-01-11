using System;
using Moq;
using Stratis.SmartContracts.CLR.Serialization;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class SerializerTests
    {
        private readonly Serializer serializer;
        private readonly Mock<IContractPrimitiveSerializer> contractPrimitiveSerializer;

        public SerializerTests()
        {
            this.contractPrimitiveSerializer = new Mock<IContractPrimitiveSerializer>();
            this.serializer = new Serializer(this.contractPrimitiveSerializer.Object);
        }

        [Fact]
        public void Serialize_Null_String_Returns_Null()
        {
            var val = (string) null;
            var result = this.serializer.Serialize(val);

            Assert.Null(result);
            this.contractPrimitiveSerializer.Verify(s => s.Serialize(val), Times.Never);
        }

        [Fact]
        public void Serialize_Null_Array_Returns_Null()
        {
            var val = (Array) null;
            var result = this.serializer.Serialize(val);

            Assert.Null(result);
            this.contractPrimitiveSerializer.Verify<byte[]>(s => s.Serialize(val), Times.Never);
        }

        [Fact]
        public void Deserialize_Null_Bool_Returns_Default()
        {
            var result = this.serializer.ToBool(null);

            Assert.Equal(default(bool), result);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<bool>(It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Deserialize_Null_Address_Returns_Default()
        {
            var result = this.serializer.ToAddress((string) null);

            Assert.Equal(default(Address), result);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<Address>(It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Deserialize_Null_Int32_Returns_Default()
        {
            var result = this.serializer.ToInt32(null);

            Assert.Equal(default(int), result);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<int>(It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Deserialize_Null_UInt32_Returns_Default()
        {
            var result = this.serializer.ToUInt32(null);

            Assert.Equal(default(uint), result);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<uint>(It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Deserialize_Null_Int64_Returns_Default()
        {
            var result = this.serializer.ToInt64(null);

            Assert.Equal(default(long), result);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<long>(It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Deserialize_Null_UInt64_Returns_Default()
        {
            var result = this.serializer.ToUInt64(null);

            Assert.Equal(default(ulong), result);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<ulong>(It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Deserialize_Null_String_Returns_EmptyString()
        {
            var result = this.serializer.ToString(null);

            Assert.NotNull(result);
            Assert.Equal(string.Empty, result);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<string>(It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Deserialize_Null_Array_Returns_EmptyArray()
        {
            var result = this.serializer.ToArray<string>(null);

            Assert.NotNull(result);
            Assert.Equal(new string[0], result);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<string[]>(It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Deserialize_Invalid_Int32_Returns_Default()
        {
            var result = this.serializer.ToInt32(new byte[0]);

            Assert.Equal(default(int), result);

            result = this.serializer.ToInt32(new byte[1]);

            Assert.Equal(default(int), result);

            result = this.serializer.ToInt32(new byte[2]);

            Assert.Equal(default(int), result);

            result = this.serializer.ToInt32(new byte[3]);

            Assert.Equal(default(int), result);

            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<int>(It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Deserialize_LongerThanExpected_Returns_SameAsContractPrimitiveSerializer()
        {
            byte[] randomBytes = new byte[10];
            new Random().NextBytes(randomBytes);

            int intResult = this.serializer.ToInt32(randomBytes);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<int>(It.IsAny<byte[]>()), Times.Once);

            uint uintResult = this.serializer.ToUInt32(randomBytes);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<uint>(It.IsAny<byte[]>()), Times.Once);

            long longResult = this.serializer.ToInt64(randomBytes);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<long>(It.IsAny<byte[]>()), Times.Once);

            ulong ulongResult = this.serializer.ToUInt64(randomBytes);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<ulong>(It.IsAny<byte[]>()), Times.Once);
        }

        [Fact]
        public void Deserialize_Byte0_Address_Returns_Default()
        {
            var result = this.serializer.ToAddress(new byte[0]);

            Assert.Equal(default(Address), result);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<Address>(It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Deserialize_Byte0_Bool_Returns_Default()
        {
            var result = this.serializer.ToBool(new byte[0]);

            Assert.Equal(default(bool), result);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<bool>(It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Deserialize_Byte0_String_Returns_Empty()
        {
            var result = this.serializer.ToString(new byte[0]);

            Assert.Equal(string.Empty, result);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<string>(It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Deserialize_Byte0_ByteArray_Returns_Empty()
        {
            var result = this.serializer.ToArray<byte>(new byte[0]);

            Assert.Equal(new byte[0], result);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<byte[]>(It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Deserialize_Byte0_StringArray_Returns_Empty()
        {
            var result = this.serializer.ToArray<string>(new byte[0]);

            Assert.Equal(new string[0], result);
            this.contractPrimitiveSerializer.Verify(s => s.Deserialize<string[]>(It.IsAny<byte[]>()), Times.Never);
        }

        public struct Example
        {
            public int Item1;
            public int Item2;
            public string Item3;
        }

        [Fact]
        public void Deserialize_Struct_Success()
        {
            var example = new Example { Item1 = 1234, Item2 = 4567, Item3 = null };

            var item1Bytes = BitConverter.GetBytes(example.Item1);
            var item2Bytes = BitConverter.GetBytes(example.Item2);
            var item3Bytes = new byte[0];
            this.contractPrimitiveSerializer.Setup(p => p.Serialize(example.Item1)).Returns((byte[]) item1Bytes);
            this.contractPrimitiveSerializer.Setup(p => p.Serialize(example.Item2)).Returns((byte[]) item2Bytes);
            this.contractPrimitiveSerializer.Setup(p => p.Serialize(example.Item3)).Returns(item3Bytes);

            this.contractPrimitiveSerializer.Setup(p => p.Deserialize(typeof(int), item1Bytes)).Returns(example.Item1);
            this.contractPrimitiveSerializer.Setup(p => p.Deserialize(typeof(int), item2Bytes)).Returns(example.Item2);
            this.contractPrimitiveSerializer.Setup(p => p.Deserialize(typeof(string), item3Bytes)).Returns(example.Item3);

            var bytes = this.serializer.Serialize(example);
            var deserialized = this.serializer.ToStruct<Example>(bytes);

            Assert.Equal(example, deserialized);
        }

        [Fact]
        public void Deserialize_Null_To_Struct_Returns_Default()
        {
            Example deserialized = this.serializer.ToStruct<Example>(null);

            Assert.Equal(default(Example), deserialized);
        }

        [Fact]
        public void Deserialize_Byte0_To_Struct_Returns_Default()
        {
            var deserialized = this.serializer.ToStruct<Example>(new byte[0]);

            this.contractPrimitiveSerializer.Verify<object>(s => s.Deserialize(It.IsAny<Type>(), It.IsAny<byte[]>()), Times.Never);
            Assert.Equal(default(Example), deserialized);
        }

        [Fact]
        public void Deserialize_Garbage_To_Struct_Returns_Default()
        {
            // An exception should be thrown when attempting to RLP decode these bytes.
            var deserialized = this.serializer.ToStruct<Example>(new byte[] { 0x00, 0xFF, 0xAA });

            // The contract primitive serializer should not be called.
            this.contractPrimitiveSerializer.Verify<object>(s => s.Deserialize(It.IsAny<Type>(), It.IsAny<byte[]>()), Times.Never);
            Assert.Equal(default(Example), deserialized);
        }
    }
}