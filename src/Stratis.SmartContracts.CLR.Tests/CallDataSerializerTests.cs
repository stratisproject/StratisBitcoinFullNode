using System.Linq;
using System.Text;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public sealed class CallDataSerializerTests
    {
        public ICallDataSerializer Serializer = new CallDataSerializer(new ContractPrimitiveSerializer(new SmartContractsRegTest()));

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

            var contractTxData = new ContractTxData(1, 1, (RuntimeObserver.Gas)5000, contractExecutionCode);
            var callDataResult = this.Serializer.Deserialize(this.Serializer.Serialize(contractTxData));
            var callData = callDataResult.Value;

            Assert.True((bool) callDataResult.IsSuccess);
            Assert.Equal(1, callData.VmVersion);
            Assert.Equal((byte)ScOpcodeType.OP_CREATECONTRACT, callData.OpCodeType);
            Assert.Equal<byte[]>(contractExecutionCode, callData.ContractExecutionCode);
            Assert.Equal((RuntimeObserver.Gas)1, callData.GasPrice);
            Assert.Equal((RuntimeObserver.Gas)5000, callData.GasLimit);
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

            object[] methodParameters =
            {
                true,
                "te|s|t",
                "te#st",
                "#4#te#st#",
                '#'
            };

            var contractTxData = new ContractTxData(1, 1, (RuntimeObserver.Gas)5000, contractExecutionCode, methodParameters);

            var callDataResult = this.Serializer.Deserialize(this.Serializer.Serialize(contractTxData));
            var callData = callDataResult.Value;

            Assert.True((bool) callDataResult.IsSuccess);
            Assert.Equal(contractTxData.VmVersion, callData.VmVersion);
            Assert.Equal(contractTxData.OpCodeType, callData.OpCodeType);
            Assert.Equal(contractTxData.ContractExecutionCode, callData.ContractExecutionCode);
            Assert.Equal(methodParameters.Length, callData.MethodParameters.Length);

            Assert.NotNull(callData.MethodParameters[0]);
            Assert.Equal(methodParameters[0], (bool)callData.MethodParameters[0]);

            Assert.NotNull(callData.MethodParameters[1]);
            Assert.Equal(methodParameters[1], callData.MethodParameters[1]);

            Assert.NotNull(callData.MethodParameters[2]);
            Assert.Equal(methodParameters[2], callData.MethodParameters[2]);

            Assert.NotNull(callData.MethodParameters[3]);
            Assert.Equal(methodParameters[3], callData.MethodParameters[3]);

            Assert.NotNull(callData.MethodParameters[4]);
            Assert.Equal(methodParameters[4], callData.MethodParameters[4]);

            Assert.Equal(contractTxData.GasPrice, callData.GasPrice);
            Assert.Equal(contractTxData.GasLimit, callData.GasLimit);
        }

        [Fact]
        public void SmartContract_CanSerialize_OP_CALLCONTRACT_WithoutMethodParameters()
        {          
            var contractTxData = new ContractTxData(1, 1, (RuntimeObserver.Gas)5000, 100, "Execute");

            var callDataResult = this.Serializer.Deserialize(this.Serializer.Serialize(contractTxData));
            var callData = callDataResult.Value;

            Assert.True((bool) callDataResult.IsSuccess);
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
            object[] methodParameters =
            {
                true,
                (byte)1,
                Encoding.UTF8.GetBytes("test"),
                's',
                "test",
                (uint)36,
                (ulong)29,
                "0x95D34980095380851902ccd9A1Fb4C813C2cb639".HexToAddress(),
                "0x95D34980095380851902ccd9A1Fb4C813C2cb639".HexToAddress()
            };

            var contractTxData = new ContractTxData(1, 1, (RuntimeObserver.Gas)5000, 100, "Execute", methodParameters);
            var callDataResult = this.Serializer.Deserialize(this.Serializer.Serialize(contractTxData));
            var callData = callDataResult.Value;

            Assert.True((bool) callDataResult.IsSuccess);

            Assert.NotNull(callData.MethodParameters[0]);
            Assert.Equal(methodParameters[0], callData.MethodParameters[0]);

            Assert.NotNull(callData.MethodParameters[1]);
            Assert.Equal(methodParameters[1], callData.MethodParameters[1]);

            Assert.NotNull(callData.MethodParameters[2]);
            Assert.True(((byte[]) methodParameters[2]).SequenceEqual((byte[]) callData.MethodParameters[2]));

            Assert.NotNull(callData.MethodParameters[3]);
            Assert.Equal(methodParameters[3], callData.MethodParameters[3]);

            Assert.NotNull(callData.MethodParameters[4]);
            Assert.Equal(methodParameters[4], callData.MethodParameters[4]);

            Assert.NotNull(callData.MethodParameters[5]);
            Assert.Equal(methodParameters[5], callData.MethodParameters[5]);

            Assert.NotNull(callData.MethodParameters[6]);
            Assert.Equal(methodParameters[6], callData.MethodParameters[6]);

            Assert.NotNull(callData.MethodParameters[7]);
            Assert.Equal(methodParameters[7], callData.MethodParameters[7]);

            Assert.NotNull(callData.MethodParameters[8]);
            Assert.Equal(methodParameters[8], callData.MethodParameters[8]);
        }
    }
}