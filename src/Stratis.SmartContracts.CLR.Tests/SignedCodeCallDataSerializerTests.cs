using Nethereum.RLP;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.RuntimeObserver;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class SignedCodeCallDataSerializerTests
    {
        public ICallDataSerializer Serializer = new SignedCodeCallDataSerializer(new ContractPrimitiveSerializer(new SmartContractsRegTest()));

        [Fact]
        public void SmartContract_Can_Serialize_Deserialize_With_Signature()
        {
            var contractCode = new byte[] {0xAA};
            var contractSignature = new byte[] {0xBB};
            var contractBytes = RLP.EncodeList(
                RLP.EncodeElement(contractCode),
                RLP.EncodeElement(contractSignature));
            var contractTxData = new ContractTxData(1, 1, (Gas)5000, contractBytes);
            var callDataResult = this.Serializer.Deserialize(this.Serializer.Serialize(contractTxData));
            var callData = callDataResult.Value;

            Assert.True((bool)callDataResult.IsSuccess);
            Assert.Equal(1, callData.VmVersion);
            Assert.Equal((byte)ScOpcodeType.OP_CREATECONTRACT, callData.OpCodeType);
            Assert.Equal<byte[]>(contractCode, callData.ContractExecutionCode);
            Assert.Equal<byte[]>(contractSignature, ((SignedCodeContractTxData)callData).CodeSignature);
            Assert.Equal((Gas)1, callData.GasPrice);
            Assert.Equal((Gas)5000, callData.GasLimit);
        }
    }
}