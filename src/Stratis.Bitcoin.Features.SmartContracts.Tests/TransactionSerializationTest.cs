using System.Text;
using NBitcoin;
using Stratis.SmartContracts;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class TransactionSerializationTest
    {
        [Fact]
        public void SmartContract_CanSerialize_OP_CREATECONTRACT()
        {
            byte[] contractExecutionCode = Encoding.UTF8.GetBytes(
@"
using System;
using Stratis.SmartContracts;

[References]
public class Test : CompiledSmartContract
{
    public void TestMethod()
    {
        [CodeToExecute]
    }
}");
            var smartContractCarrier = SmartContractCarrier.CreateContract(1, contractExecutionCode, 1, 500000);
            byte[] smartContractCarrierSerialized = smartContractCarrier.Serialize();

            var tx = new Transaction();
            tx.AddInput(new TxIn(new OutPoint(0, 0), new Script(OpcodeType.OP_1)));
            tx.AddOutput(new TxOut(new Money(5000000000L - 10000), new Script(smartContractCarrierSerialized)));

            var deserialized = SmartContractCarrier.Deserialize(tx.GetHash(), tx.Outputs[0].ScriptPubKey, tx.Outputs[0].Value);
            Assert.Equal(smartContractCarrier.VmVersion, deserialized.VmVersion);
            Assert.Equal(smartContractCarrier.OpCodeType, deserialized.OpCodeType);
            Assert.Equal(smartContractCarrier.ContractExecutionCode, deserialized.ContractExecutionCode);
            Assert.Equal(smartContractCarrier.GasPrice, deserialized.GasPrice);
            Assert.Equal(smartContractCarrier.GasLimit, deserialized.GasLimit);
        }

        [Fact]
        public void SmartContract_CanSerialize_OP_CALLCONTRACT()
        {
            var methodParameters = string.Join('|', new string[] { "int#1", "string#test" });

            var smartContractCarrier = SmartContractCarrier.CallContract(1, 100, "Execute", methodParameters, 1, 500000);

            var tx = new Transaction();
            tx.AddInput(new TxIn(new OutPoint(0, 0), new Script(OpcodeType.OP_1)));
            tx.AddOutput(new TxOut(new Money(5000000000L - 10000), new Script(smartContractCarrier.Serialize())));

            var deserialized = SmartContractCarrier.Deserialize(tx.GetHash(), tx.Outputs[0].ScriptPubKey, tx.Outputs[0].Value);
            Assert.Equal(smartContractCarrier.VmVersion, deserialized.VmVersion);
            Assert.Equal(smartContractCarrier.OpCodeType, deserialized.OpCodeType);
            Assert.Equal(smartContractCarrier.To, deserialized.To);
            Assert.Equal(smartContractCarrier.MethodName, deserialized.MethodName);
            Assert.Equal(smartContractCarrier.MethodParameters[0], deserialized.MethodParameters[0]);
            Assert.Equal(smartContractCarrier.MethodParameters[1], deserialized.MethodParameters[1]);
            Assert.Equal(smartContractCarrier.GasPrice, deserialized.GasPrice);
            Assert.Equal(smartContractCarrier.GasLimit, deserialized.GasLimit);
        }
    }
}