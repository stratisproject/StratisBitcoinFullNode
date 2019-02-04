using System.Collections.Generic;
using CSharpFunctionalExtensions;
using Stratis.SmartContracts.CLR.Serialization;

namespace Stratis.SmartContracts.CLR
{
    public class SignedCodeCallDataSerializer : CallDataSerializer
    {
        public SignedCodeCallDataSerializer(IContractPrimitiveSerializer primitiveSerializer)
            : base(primitiveSerializer)
        {
        }

        protected override Result<ContractTxData> SerializeCreateContract(byte[] smartContractBytes, int vmVersion, ulong gasPrice, RuntimeObserver.Gas gasLimit)
        {
            var remaining = smartContractBytes.Slice(PrefixSize, (uint)(smartContractBytes.Length - PrefixSize));

            IList<byte[]> decodedParams = RLPDecode(remaining);

            var codeAndSignature = decodedParams[0];

            IList<byte[]> decodedCodeAndSignature = RLPDecode(codeAndSignature);

            var contractExecutionCode = decodedCodeAndSignature[0];
            var signature = decodedCodeAndSignature[1];

            var methodParameters = this.DeserializeMethodParameters(decodedParams[1]);

            var callData = new SignedCodeContractTxData(vmVersion, gasPrice, gasLimit, contractExecutionCode, signature, methodParameters);
            return Result.Ok<ContractTxData>(callData);
        }
    }
}