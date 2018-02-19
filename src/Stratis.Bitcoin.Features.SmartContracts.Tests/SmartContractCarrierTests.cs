using System;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class SmartContractCarrierTests
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

            var smartContractCarrier = SmartContractCarrier.CreateContract(1, contractExecutionCode, 1, 500000);
            byte[] smartContractCarrierSerialized = smartContractCarrier.Serialize();

            var tx = new Transaction();
            tx.AddInput(new TxIn(new OutPoint(0, 0), new Script(OpcodeType.OP_1)));
            tx.AddOutput(new TxOut(new Money(5000000000L - 10000), new Script(smartContractCarrierSerialized)));

            var deserialized = SmartContractCarrier.Deserialize(tx, tx.Outputs[0]);
            Assert.Equal(smartContractCarrier.VmVersion, deserialized.VmVersion);
            Assert.Equal(smartContractCarrier.OpCodeType, deserialized.OpCodeType);
            Assert.Equal(smartContractCarrier.ContractExecutionCode, deserialized.ContractExecutionCode);
            Assert.Equal(smartContractCarrier.GasPrice, deserialized.GasPrice);
            Assert.Equal(smartContractCarrier.GasLimit, deserialized.GasLimit);

            Assert.True(tx.Outputs[0].ScriptPubKey.IsSmartContractExec);
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

            string[] testMethodParameters = new string[]
            {
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Short, 12),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Bool, true),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.String, "te|s|t"),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.String, "te#st"),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.String, "#4#te#st#"),
            };

            SmartContractCarrier smartContractCarrier = SmartContractCarrier.CreateContract(1, contractExecutionCode, 1, 500000).WithParameters(testMethodParameters);
            byte[] smartContractCarrierSerialized = smartContractCarrier.Serialize();

            var tx = new Transaction();
            tx.AddInput(new TxIn(new OutPoint(0, 0), new Script(OpcodeType.OP_1)));
            tx.AddOutput(new TxOut(new Money(5000000000L - 10000), new Script(smartContractCarrierSerialized)));

            var deserialized = SmartContractCarrier.Deserialize(tx, tx.Outputs[0]);
            Assert.Equal(smartContractCarrier.VmVersion, deserialized.VmVersion);
            Assert.Equal(smartContractCarrier.OpCodeType, deserialized.OpCodeType);
            Assert.Equal(smartContractCarrier.ContractExecutionCode, deserialized.ContractExecutionCode);
            Assert.Equal(5, deserialized.MethodParameters.Length);

            Assert.NotNull(deserialized.MethodParameters[0]);
            Assert.Equal(smartContractCarrier.MethodParameters[0], deserialized.MethodParameters[0]);
            Assert.Equal(12, deserialized.MethodParameters[0]);

            Assert.NotNull(deserialized.MethodParameters[1]);
            Assert.Equal(smartContractCarrier.MethodParameters[1], deserialized.MethodParameters[1]);
            Assert.True((bool)deserialized.MethodParameters[1]);

            Assert.NotNull(deserialized.MethodParameters[2]);
            Assert.Equal(smartContractCarrier.MethodParameters[2], deserialized.MethodParameters[2]);
            Assert.Equal("te|s|t", deserialized.MethodParameters[2]);

            Assert.NotNull(deserialized.MethodParameters[3]);
            Assert.Equal(smartContractCarrier.MethodParameters[3], deserialized.MethodParameters[3]);
            Assert.Equal("te#st", deserialized.MethodParameters[3]);

            Assert.NotNull(deserialized.MethodParameters[4]);
            Assert.Equal(smartContractCarrier.MethodParameters[4], deserialized.MethodParameters[4]);
            Assert.Equal("#4#te#st#", deserialized.MethodParameters[4]);

            Assert.Equal(smartContractCarrier.GasPrice, deserialized.GasPrice);
            Assert.Equal(smartContractCarrier.GasLimit, deserialized.GasLimit);
        }

        [Fact]
        public void SmartContract_CanSerialize_OP_CALLCONTRACT_WithoutMethodParameters()
        {
            SmartContractCarrier smartContractCarrier = SmartContractCarrier.CallContract(1, 100, "Execute", 1, 500000);

            var tx = new Transaction();
            tx.AddInput(new TxIn(new OutPoint(0, 0), new Script(OpcodeType.OP_1)));
            tx.AddOutput(new TxOut(new Money(5000000000L - 10000), new Script(smartContractCarrier.Serialize())));

            var deserialized = SmartContractCarrier.Deserialize(tx, tx.Outputs[0]);

            Assert.Equal(smartContractCarrier.VmVersion, deserialized.VmVersion);
            Assert.Equal(smartContractCarrier.OpCodeType, deserialized.OpCodeType);
            Assert.Equal(smartContractCarrier.To, deserialized.To);
            Assert.Equal(smartContractCarrier.MethodName, deserialized.MethodName);
            Assert.Null(deserialized.MethodParameters);
            Assert.Equal(smartContractCarrier.GasPrice, deserialized.GasPrice);
            Assert.Equal(smartContractCarrier.GasLimit, deserialized.GasLimit);
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
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Address, new Address("0x95D34980095380851902ccd9A1Fb4C813C2cb639"))
            };

            SmartContractCarrier smartContractCarrier = SmartContractCarrier.CallContract(1, 100, "Execute", 1, 500000).WithParameters(methodParameters);

            var tx = new Transaction();
            tx.AddInput(new TxIn(new OutPoint(0, 0), new Script(OpcodeType.OP_1)));
            tx.AddOutput(new TxOut(new Money(5000000000L - 10000), new Script(smartContractCarrier.Serialize())));

            var deserialized = SmartContractCarrier.Deserialize(tx, tx.Outputs[0]);

            Assert.NotNull(deserialized.MethodParameters[0]);
            Assert.Equal(smartContractCarrier.MethodParameters[0], deserialized.MethodParameters[0]);

            Assert.NotNull(deserialized.MethodParameters[1]);
            Assert.Equal(smartContractCarrier.MethodParameters[1], deserialized.MethodParameters[1]);

            Assert.NotNull(deserialized.MethodParameters[2]);
            Assert.Equal(smartContractCarrier.MethodParameters[2], deserialized.MethodParameters[2]);

            Assert.NotNull(deserialized.MethodParameters[3]);
            Assert.Equal(smartContractCarrier.MethodParameters[3], deserialized.MethodParameters[3]);

            Assert.NotNull(deserialized.MethodParameters[4]);
            Assert.Equal(smartContractCarrier.MethodParameters[4], deserialized.MethodParameters[4]);

            Assert.NotNull(deserialized.MethodParameters[5]);
            Assert.Equal(smartContractCarrier.MethodParameters[5], deserialized.MethodParameters[5]);

            Assert.NotNull(deserialized.MethodParameters[6]);
            Assert.Equal(smartContractCarrier.MethodParameters[6], deserialized.MethodParameters[6]);

            Assert.NotNull(deserialized.MethodParameters[7]);
            Assert.Equal(smartContractCarrier.MethodParameters[7], deserialized.MethodParameters[7]);

            Assert.NotNull(deserialized.MethodParameters[8]);
            Assert.Equal(smartContractCarrier.MethodParameters[8], deserialized.MethodParameters[8]);

            Assert.NotNull(deserialized.MethodParameters[9]);
            Assert.Equal(smartContractCarrier.MethodParameters[9], deserialized.MethodParameters[9]);

            Assert.NotNull(deserialized.MethodParameters[10]);
            Assert.Equal(smartContractCarrier.MethodParameters[10], deserialized.MethodParameters[10]);
        }
    }
}