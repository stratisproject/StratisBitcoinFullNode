using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class MethodParameterByteSerializerTests
    {
        public static Network Network = new SmartContractPosRegTest();
        public IMethodParameterSerializer Serializer = new MethodParameterByteSerializer(new ContractPrimitiveSerializer(Network));

        [Theory]
        [MemberData(nameof(GetData), parameters: 1)]
        public void Roundtrip_Method_Param_Successfully(object value)
        {
            // Roundtrip serialization
            var methodParamObjects = this.Serializer.Deserialize(this.Serializer.Serialize(new[] { value }));

            Type paramType = value.GetType();

            // Equality comparison using .Equal is possible for these Types
            if (paramType.IsValueType || paramType == typeof(string))
            {
                Assert.Equal(value, methodParamObjects[0]);
            }

            // For byte arrays we must compare each element
            if (paramType.IsArray && paramType.GetElementType() == typeof(byte))
            {
                Assert.True(((byte[])value).SequenceEqual((byte[])methodParamObjects[0]));
            }
        }

        [Fact]
        public void Serialized_Method_Params_Are_Smaller_Than_Strings()
        {
            // Single comparative case for a sample byte vs. string encoded method params array
            var stringSerializer = new MethodParameterStringSerializer(Network);

            var parameters = Enumerable.SelectMany<object[], object>(GetData(0), o => o).ToArray();

            var serializedBytes = this.Serializer.Serialize(parameters);
            var s = stringSerializer.Serialize(parameters);
            var serializedString = Encoding.UTF8.GetBytes(s);
            
            Assert.True(serializedBytes.Length <= serializedString.Length);
        }

        [Fact]
        public void Roundtrip_Serialize_Multiple_Params()
        {
            object[] methodParameters = Enumerable.SelectMany<object[], object>(GetData(0), o => o).ToArray();

            var serialized = this.Serializer.Serialize(methodParameters);

            var deserialized = this.Serializer.Deserialize(serialized);

            Assert.Equal(deserialized.Length, methodParameters.Length);

            for (var i = 0; i < deserialized.Length; i++)
            {
                Assert.Equal(methodParameters[i], deserialized[i]);
            }
        }

        [Fact]
        public void Deserialize_Garbage_Throws()
        {
            Assert.ThrowsAny<Exception>(() => this.Serializer.Deserialize(new byte[] { 0x00, 0x11, 0x22, 0x33 }));
        }


        public static IEnumerable<object[]> GetData(int numTests)
        {
            yield return new object[] { true }; // MethodParameterDataType.Bool
            yield return new object[] { (byte)1 }; // MethodParameterDataType.Byte
            yield return new object[] { Encoding.UTF8.GetBytes("test") }; // MethodParameterDataType.ByteArray
            yield return new object[] { 's' }; // MethodParameterDataType.Char
            yield return new object[] { "test" }; // MethodParameterDataType.String
            yield return new object[] { (uint)36 }; // MethodParameterDataType.UInt
            yield return new object[] { (ulong)29 }; // MethodParameterDataType.ULong
            yield return new object[] { "0x0000000000000000000000000000000000000001".HexToAddress() }; // MethodParameterDataType.Address
            yield return new object[] { (long)12312321 }; // MethodParameterDataType.Long,
            yield return new object[] { (int)10000000 };// MethodParameterDataType.Int
        }
    }
}