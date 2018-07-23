using System;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class CallDataSerializerTests
    {
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

            var carrier = SmartContractCarrier.CreateContract(1, contractExecutionCode, 1, (Gas)5000);

            var serializer = CallDataSerializer.Default;

            var callDataResult = serializer.Deserialize(carrier.Serialize());
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
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Short, 12),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Bool, true),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.String, "te|s|t"),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.String, "te#st"),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.String, "#4#te#st#"),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Char, '#'),
            };

            var carrier = SmartContractCarrier.CreateContract(1, contractExecutionCode, 1, (Gas)500000, methodParameters);

            var serializer = CallDataSerializer.Default;

            var callDataResult = serializer.Deserialize(carrier.Serialize());
            var callData = callDataResult.Value;

            Assert.True(callDataResult.IsSuccess);
            Assert.Equal(carrier.ContractTxData.VmVersion, callData.VmVersion);
            Assert.Equal(carrier.ContractTxData.OpCodeType, callData.OpCodeType);            
            Assert.Equal(carrier.ContractTxData.ContractExecutionCode, callData.ContractExecutionCode);
            Assert.Equal(6, callData.MethodParameters.Length);

            Assert.NotNull(callData.MethodParameters[0]);
            Assert.Equal(12, callData.MethodParameters[0]);

            Assert.NotNull(carrier.MethodParameters[1]);
            Assert.True((bool)callData.MethodParameters[1]);

            Assert.NotNull(callData.MethodParameters[2]);
            Assert.Equal("te|s|t", callData.MethodParameters[2]);

            Assert.NotNull(callData.MethodParameters[3]);
            Assert.Equal("te#st", callData.MethodParameters[3]);

            Assert.NotNull(callData.MethodParameters[4]);
            Assert.Equal("#4#te#st#", callData.MethodParameters[4]);

            Assert.NotNull(callData.MethodParameters[5]);
            Assert.Equal("#", callData.MethodParameters[5]);

            Assert.Equal(carrier.ContractTxData.GasPrice, callData.GasPrice);
            Assert.Equal(carrier.ContractTxData.GasLimit, callData.GasLimit);
        }

        [Fact]
        public void SmartContract_CanSerialize_OP_CALLCONTRACT_WithoutMethodParameters()
        {
            var smartContractCarrier = SmartContractCarrier.CallContract(1, 100, "Execute", 1, (Gas)500000);
            
            var serializer = CallDataSerializer.Default;

            var callDataResult = serializer.Deserialize(smartContractCarrier.Serialize());
            var callData = callDataResult.Value;

            Assert.True(callDataResult.IsSuccess);
            Assert.Equal(smartContractCarrier.ContractTxData.VmVersion, callData.VmVersion);
            Assert.Equal(smartContractCarrier.ContractTxData.OpCodeType, callData.OpCodeType);
            Assert.Equal(smartContractCarrier.ContractTxData.ContractAddress, callData.ContractAddress);
            Assert.Equal(smartContractCarrier.ContractTxData.MethodName, callData.MethodName);
            Assert.Equal(smartContractCarrier.ContractTxData.GasPrice, callData.GasPrice);
            Assert.Equal(smartContractCarrier.ContractTxData.GasLimit, callData.GasLimit);
        }

        [Fact]
        public void SmartContract_CanSerialize_OP_CALLCONTRACT_WithMethodParameters()
        {
            string[] methodParameters = new string[]
            {
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Bool, true),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Byte, (byte)1),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.ByteArray, BitConverter.ToString(Encoding.UTF8.GetBytes("test"))),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Char, 's'),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.SByte, -45),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Short, 7),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.String, "test"),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.UInt, 36),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.UInt160, new uint160(new byte[20]{ 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1})),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.ULong, 29),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Address, new Address("0x95D34980095380851902ccd9A1Fb4C813C2cb639")),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Address, new Address("mxKorCkWmtrPoekfWiMzERJPhaT13nnkMy"))
            };

            var carrier = SmartContractCarrier.CallContract(1, 100, "Execute", 1, (Gas)500000, methodParameters);

            var serializer = CallDataSerializer.Default;

            var callDataResult = serializer.Deserialize(carrier.Serialize());
            var callData = callDataResult.Value;

            Assert.True(callDataResult.IsSuccess);

            Assert.NotNull(callData.MethodParameters[0]);
            Assert.Equal(carrier.MethodParameters[0], callData.MethodParameters[0]);

            Assert.NotNull(callData.MethodParameters[1]);
            Assert.Equal(carrier.MethodParameters[1], callData.MethodParameters[1]);

            Assert.NotNull(callData.MethodParameters[2]);
            Assert.Equal(carrier.MethodParameters[2], callData.MethodParameters[2]);

            Assert.NotNull(callData.MethodParameters[3]);
            Assert.Equal(carrier.MethodParameters[3], callData.MethodParameters[3]);

            Assert.NotNull(callData.MethodParameters[4]);
            Assert.Equal(carrier.MethodParameters[4], callData.MethodParameters[4]);

            Assert.NotNull(callData.MethodParameters[5]);
            Assert.Equal(carrier.MethodParameters[5], callData.MethodParameters[5]);

            Assert.NotNull(callData.MethodParameters[6]);
            Assert.Equal(carrier.MethodParameters[6], callData.MethodParameters[6]);

            Assert.NotNull(callData.MethodParameters[7]);
            Assert.Equal(carrier.MethodParameters[7], callData.MethodParameters[7]);

            Assert.NotNull(callData.MethodParameters[8]);
            Assert.Equal(carrier.MethodParameters[8], callData.MethodParameters[8]);

            Assert.NotNull(callData.MethodParameters[9]);
            Assert.Equal(carrier.MethodParameters[9], callData.MethodParameters[9]);

            Assert.NotNull(callData.MethodParameters[10]);
            Assert.Equal(carrier.MethodParameters[10], callData.MethodParameters[10]);

            Assert.NotNull(callData.MethodParameters[11]);
            Assert.Equal(carrier.MethodParameters[11], callData.MethodParameters[11]);
        }
    }
}