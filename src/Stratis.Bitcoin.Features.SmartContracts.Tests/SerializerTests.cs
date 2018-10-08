using System;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
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
            this.contractPrimitiveSerializer.Verify(s => s.Serialize(val), Times.Never);
        }

        [Fact]
        public void Serialize_Address_With_Null_Value_Returns_Null()
        {
            var val = new Address();
            var result = this.serializer.Serialize(val);

            Assert.Null(result);
            this.contractPrimitiveSerializer.Verify(s => s.Serialize(val), Times.Never);
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
            var result = this.serializer.ToAddress(null);

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
    }
}