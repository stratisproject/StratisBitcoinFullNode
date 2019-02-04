using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class SignedCodeCallDataSerializerTests
    {
        public ICallDataSerializer Serializer = new SignedCodeCallDataSerializer(new ContractPrimitiveSerializer(new SmartContractsRegTest()));

        [Fact]
        public void SmartContract_Can_Serialize_Deserialize_With_Signature()
        {
            var contractTxData = new SignedCodeContractTxData(1, 1, (RuntimeObserver.Gas)5000, new byte[] { 0xAA }, new byte[] { 0xBB});
            var callDataResult = this.Serializer.Deserialize(this.Serializer.Serialize(contractTxData));
            var callData = callDataResult.Value;

            Assert.True((bool)callDataResult.IsSuccess);
            Assert.Equal(1, callData.VmVersion);
            Assert.Equal((byte)ScOpcodeType.OP_CREATECONTRACT, callData.OpCodeType);
            Assert.Equal<byte[]>(contractTxData.ContractExecutionCode, callData.ContractExecutionCode);
            Assert.Equal<byte[]>(contractTxData.CodeSignature, ((SignedCodeContractTxData)callData).CodeSignature);
            Assert.Equal((RuntimeObserver.Gas)1, callData.GasPrice);
            Assert.Equal((RuntimeObserver.Gas)5000, callData.GasLimit);
        }
    }
}