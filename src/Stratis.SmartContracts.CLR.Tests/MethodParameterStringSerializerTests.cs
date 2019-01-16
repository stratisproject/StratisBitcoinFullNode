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
    public class MethodParameterStringSerializerTests
    {
        public static Network Network = new SmartContractsRegTest();
        public static IMethodParameterStringSerializer Serializer = new MethodParameterStringSerializer(Network);

        [Theory]
        [MemberData(nameof(GetData), parameters: 1)]
        public void Roundtrip_Method_Param_Successfully(object value)
        {
            // Roundtrip serialization
            var methodParamObjects = Serializer.Deserialize(Serializer.Serialize(new [] { value }));

            Type paramType = value.GetType();

            // Equality comparison using .Equal is possible for these Types
            if (paramType.IsValueType || paramType == typeof(string))
            {
                Assert.Equal(value, methodParamObjects[0]);
            }

            // For byte arrays we must compare each element
            if (paramType.IsArray && paramType.GetElementType() == typeof(byte))
            {
                Assert.True(((byte[])value).SequenceEqual((byte[]) methodParamObjects[0]));
            }
        }

        [Fact]
        public void Serialized_Address_Is_Base58()
        {
            var address = new Address();

            var serializedAddress = MethodParameterStringSerializer.Serialize(address, Network);
            
            // Will throw if address is invalid
            BitcoinAddress.Create(serializedAddress, Network);        
        }

        [Fact]
        public void Serialize_Multiple_Params()
        {
            object[] methodParameters =
            {
                (int) 12,
                true,
                "te|s|t",
                "te#st",
                "#4#te#st#",
                '#'
            };

            var serialized = Serializer.Serialize(methodParameters);

            Assert.Equal("6#12|1#True|4#te\\|s\\|t|4#te\\#st|4#\\#4\\#te\\#st\\#|3#\\#", serialized);
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
            yield return new object[] { new uint160("0x0000000000000000000000000000000000000001").ToBase58Address(Network) }; // MethodParameterDataType.Address
            yield return new object[] { (long)12312321 }; // MethodParameterDataType.Long,
            yield return new object[] { (int)10000000 };// MethodParameterDataType.Int
        }
    }
}