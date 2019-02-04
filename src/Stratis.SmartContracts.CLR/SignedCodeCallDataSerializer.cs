using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using Nethereum.RLP;
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

            var contractExecutionCode = decodedParams[0];
            var signature = decodedParams[1];
            var methodParameters = this.DeserializeMethodParameters(decodedParams[2]);

            var callData = new SignedCodeContractTxData(vmVersion, gasPrice, gasLimit, contractExecutionCode, signature, methodParameters);
            return Result.Ok<ContractTxData>(callData);
        }

        protected override byte[] SerializeCreateContract(ContractTxData contractTxData)
        {            
            var rlpBytes = new List<byte[]>();

            rlpBytes.Add(contractTxData.ContractExecutionCode);

            if (contractTxData is SignedCodeContractTxData signed)
            {
                rlpBytes.Add(signed.CodeSignature);
            }

            this.AddMethodParams(rlpBytes, contractTxData.MethodParameters);

            var encoded = RLP.EncodeList(rlpBytes.Select(RLP.EncodeElement).ToArray());

            var bytes = new byte[PrefixSize + encoded.Length];

            this.SerializePrefix(bytes, contractTxData);

            encoded.CopyTo(bytes, PrefixSize);

            return bytes;
        }
    }
}