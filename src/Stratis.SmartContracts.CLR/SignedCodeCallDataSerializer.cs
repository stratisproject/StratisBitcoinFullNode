using System.Collections.Generic;
using CSharpFunctionalExtensions;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    public class SignedCodeCallDataSerializer : CallDataSerializer
    {
        public SignedCodeCallDataSerializer(IContractPrimitiveSerializer primitiveSerializer)
            : base(primitiveSerializer)
        {
        }

        protected override Result<ContractTxData> SerializeCreateContract(byte[] smartContractBytes, int vmVersion, ulong gasPrice, Gas gasLimit)
        {
            byte[] remaining = smartContractBytes.Slice(PrefixSize, (uint)(smartContractBytes.Length - PrefixSize));

            IList<byte[]> decodedParams = RLPDecode(remaining);

            byte[] codeAndSignature = decodedParams[0];

            IList<byte[]> decodedCodeAndSignature = RLPDecode(codeAndSignature);

            byte[] contractExecutionCode = decodedCodeAndSignature[0];
            byte[] signature = decodedCodeAndSignature[1];

            object[] methodParameters = this.DeserializeMethodParameters(decodedParams[1]);

            var callData = new SignedCodeContractTxData(vmVersion, gasPrice, gasLimit, contractExecutionCode, signature, methodParameters);
            return Result.Ok<ContractTxData>(callData);
        }
    }
}