using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class MethodParameterStringSerializerTests
    {
        public IMethodParameterSerializer Serializer = new MethodParameterStringSerializer();

        [Theory]
        [MemberData(nameof(GetData), parameters: 1)]
        public void Roundtrip_Method_Param_Successfully(object value)
        {
            // Roundtrip serialization
            var methodParamObjects = this.Serializer.Deserialize(this.Serializer.Serialize(new [] { value }));

            Type paramType = value.GetType();
                
            // Equality comparison using .Equal is possible for these Types
            if (paramType.IsValueType || paramType == typeof(uint160) || paramType == typeof(string))
            {
                Assert.Equal(value, methodParamObjects[0]);
            }

            // For byte arrays we must compare each element
            if (paramType.IsArray && paramType.GetElementType() == typeof(byte))
            {
                Assert.True(((byte[])value).SequenceEqual((byte[]) methodParamObjects[0]));
            }
        }

        public static IEnumerable<object[]> GetData(int numTests)
        {
            yield return new object[] { true }; // MethodParameterDataType.Bool
            yield return new object[] { (byte)1 }; // MethodParameterDataType.Byte
            yield return new object[] { Encoding.UTF8.GetBytes("test") }; // MethodParameterDataType.ByteArray
            yield return new object[] { 's' }; // MethodParameterDataType.Char
            yield return new object[] { (sbyte)-45 }; // MethodParameterDataType.SByte
            yield return new object[] { (short)7 }; // MethodParameterDataType.Short
            yield return new object[] { "test" }; // MethodParameterDataType.String
            yield return new object[] { (uint)36 }; // MethodParameterDataType.UInt
            yield return new object[] { new uint160(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }) }; // MethodParameterDataType.UInt160
            yield return new object[] { (ulong)29 }; // MethodParameterDataType.ULong
            yield return new object[] { new Address("0x95D34980095380851902ccd9A1Fb4C813C2cb639") }; // MethodParameterDataType.Address
            yield return new object[] { (long)12312321 }; // MethodParameterDataType.Long,
            yield return new object[] { (int)10000000 };// MethodParameterDataType.Int
        }
    }
}