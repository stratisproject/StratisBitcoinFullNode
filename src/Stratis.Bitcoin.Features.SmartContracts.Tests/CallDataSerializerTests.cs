using System;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class CallDataSerializerTests
    {
        public ICallDataSerializer Serializer = new CallDataSerializer(new MethodParameterSerializer());

        [Fact]
        public void SmartContract_CanSerialize_OP_CREATECONTRACT_WithoutMethodParameters()
        {
            byte[] contractExecutionCode = Encoding.UTF8.GetBytes(
                @"
                using System;
                using Stratis.SmartContracts;
                [References]

                public class Test : SmartContract
                { 
                    public void TestMethod()
                    {
                        [CodeToExecute]
                    }
                }"
            );
            
            var contractTxData = new ContractTxData(1, 1, (Gas)5000, contractExecutionCode);
            var callDataResult = this.Serializer.Deserialize(this.Serializer.Serialize(contractTxData));
            var callData = callDataResult.Value;

            Assert.True(callDataResult.IsSuccess);
            Assert.Equal(1, callData.VmVersion);
            Assert.Equal((byte)ScOpcodeType.OP_CREATECONTRACT, callData.OpCodeType);
            Assert.Equal(contractExecutionCode, callData.ContractExecutionCode);
            Assert.Equal((Gas)1, callData.GasPrice);
            Assert.Equal((Gas)5000, callData.GasLimit);
        }

        [Fact]
        public void SmartContract_CanSerialize_OP_CREATECONTRACT_WithMethodParameters()
        {
            byte[] contractExecutionCode = Encoding.UTF8.GetBytes(
                @"
                using System;
                using Stratis.SmartContracts;
                [References]

                public class Test : SmartContract
                { 
                    public void TestMethod(int orders, bool canOrder)
                    {
                        [CodeToExecute]
                    }
                }"
            );

            string[] methodParameters = new string[]
            {
                string.Format("{0}#{1}", (int)MethodParameterDataType.Short, 12),
                string.Format("{0}#{1}", (int)MethodParameterDataType.Bool, true),
                string.Format("{0}#{1}", (int)MethodParameterDataType.String, "te|s|t"),
                string.Format("{0}#{1}", (int)MethodParameterDataType.String, "te#st"),
                string.Format("{0}#{1}", (int)MethodParameterDataType.String, "#4#te#st#"),
                string.Format("{0}#{1}", (int)MethodParameterDataType.Char, '#'),
            };

            var contractTxData = new ContractTxData(1, 1, (Gas)5000, contractExecutionCode, this.Serializer.MethodParamSerializer.Deserialize(methodParameters));

            var callDataResult = this.Serializer.Deserialize(this.Serializer.Serialize(contractTxData));
            var callData = callDataResult.Value;

            Assert.True(callDataResult.IsSuccess);
            Assert.Equal(contractTxData.VmVersion, callData.VmVersion);
            Assert.Equal(contractTxData.OpCodeType, callData.OpCodeType);            
            Assert.Equal(contractTxData.ContractExecutionCode, callData.ContractExecutionCode);
            Assert.Equal(6, callData.MethodParameters.Length);

            Assert.NotNull(callData.MethodParameters[0]);
            Assert.Equal(12, callData.MethodParameters[0]);

            Assert.NotNull(callData.MethodParameters[1]);
            Assert.True((bool)callData.MethodParameters[1]);

            Assert.NotNull(callData.MethodParameters[2]);
            Assert.Equal("te|s|t", callData.MethodParameters[2]);

            Assert.NotNull(callData.MethodParameters[3]);
            Assert.Equal("te#st", callData.MethodParameters[3]);

            Assert.NotNull(callData.MethodParameters[4]);
            Assert.Equal("#4#te#st#", callData.MethodParameters[4]);

            Assert.NotNull(callData.MethodParameters[5]);
            Assert.Equal("#", callData.MethodParameters[5]);

            Assert.Equal(contractTxData.GasPrice, callData.GasPrice);
            Assert.Equal(contractTxData.GasLimit, callData.GasLimit);
        }

        [Fact]
        public void SmartContract_CanSerialize_OP_CALLCONTRACT_WithoutMethodParameters()
        {          
            var contractTxData = new ContractTxData(1, 1, (Gas)5000, 100, "Execute");

            var callDataResult = this.Serializer.Deserialize(this.Serializer.Serialize(contractTxData));
            var callData = callDataResult.Value;

            Assert.True(callDataResult.IsSuccess);
            Assert.Equal(contractTxData.VmVersion, callData.VmVersion);
            Assert.Equal(contractTxData.OpCodeType, callData.OpCodeType);
            Assert.Equal(contractTxData.ContractAddress, callData.ContractAddress);
            Assert.Equal(contractTxData.MethodName, callData.MethodName);
            Assert.Equal(contractTxData.GasPrice, callData.GasPrice);
            Assert.Equal(contractTxData.GasLimit, callData.GasLimit);
        }

        [Fact]
        public void SmartContract_CanSerialize_OP_CALLCONTRACT_WithMethodParameters()
        {
            string[] methodParameters = new string[]
            {
                string.Format("{0}#{1}", (int)MethodParameterDataType.Bool, true),
                string.Format("{0}#{1}", (int)MethodParameterDataType.Byte, (byte)1),
                string.Format("{0}#{1}", (int)MethodParameterDataType.ByteArray, BitConverter.ToString(Encoding.UTF8.GetBytes("test"))),
                string.Format("{0}#{1}", (int)MethodParameterDataType.Char, 's'),
                string.Format("{0}#{1}", (int)MethodParameterDataType.SByte, -45),
                string.Format("{0}#{1}", (int)MethodParameterDataType.Short, 7),
                string.Format("{0}#{1}", (int)MethodParameterDataType.String, "test"),
                string.Format("{0}#{1}", (int)MethodParameterDataType.UInt, 36),
                string.Format("{0}#{1}", (int)MethodParameterDataType.UInt160, new uint160(new byte[20]{ 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1})),
                string.Format("{0}#{1}", (int)MethodParameterDataType.ULong, 29),
                string.Format("{0}#{1}", (int)MethodParameterDataType.Address, new Address("0x95D34980095380851902ccd9A1Fb4C813C2cb639")),
                string.Format("{0}#{1}", (int)MethodParameterDataType.Address, new Address("mxKorCkWmtrPoekfWiMzERJPhaT13nnkMy"))
            };

            var contractTxData = new ContractTxData(1, 1, (Gas)5000, 100, "Execute", this.Serializer.MethodParamSerializer.Deserialize(methodParameters));
            var callDataResult = this.Serializer.Deserialize(this.Serializer.Serialize(contractTxData));
            var callData = callDataResult.Value;

            Assert.True(callDataResult.IsSuccess);

            Assert.NotNull(callData.MethodParameters[0]);
            Assert.Equal(true, callData.MethodParameters[0]);

            Assert.NotNull(callData.MethodParameters[1]);
            Assert.Equal((byte)1, callData.MethodParameters[1]);

            Assert.NotNull(callData.MethodParameters[2]);
            Assert.Equal(BitConverter.ToString(Encoding.UTF8.GetBytes("test")), callData.MethodParameters[2]);

            Assert.NotNull(callData.MethodParameters[3]);
            Assert.Equal("s", callData.MethodParameters[3]);

            Assert.NotNull(callData.MethodParameters[4]);
            Assert.Equal((sbyte)-45, callData.MethodParameters[4]);

            Assert.NotNull(callData.MethodParameters[5]);
            Assert.Equal((int)7, callData.MethodParameters[5]);

            Assert.NotNull(callData.MethodParameters[6]);
            Assert.Equal("test", callData.MethodParameters[6]);

            Assert.NotNull(callData.MethodParameters[7]);
            Assert.Equal((uint)36, callData.MethodParameters[7]);

            Assert.NotNull(callData.MethodParameters[8]);
            Assert.Equal(new uint160(new byte[20] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }), callData.MethodParameters[8]);

            Assert.NotNull(callData.MethodParameters[9]);
            Assert.Equal((ulong)29, callData.MethodParameters[9]);

            Assert.NotNull(callData.MethodParameters[10]);
            Assert.Equal(new Address("0x95D34980095380851902ccd9A1Fb4C813C2cb639"), callData.MethodParameters[10]);

            Assert.NotNull(callData.MethodParameters[11]);
            Assert.Equal(new Address("mxKorCkWmtrPoekfWiMzERJPhaT13nnkMy"), callData.MethodParameters[11]);
        }
    }
}