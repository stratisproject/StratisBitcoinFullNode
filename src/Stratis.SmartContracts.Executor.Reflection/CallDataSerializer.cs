using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using NBitcoin;
using Nethereum.RLP;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class CallDataSerializer : ICallDataSerializer
    {
        private readonly IMethodParameterSerializer methodParamSerializer;
        private readonly IContractPrimitiveSerializer primitiveSerializer;

        public CallDataSerializer(IMethodParameterSerializer methodParameterSerializer)
        {
            this.methodParamSerializer = methodParameterSerializer;
            this.primitiveSerializer = this.methodParamSerializer.PrimitiveSerializer;
        }

        public Result<ContractTxData> Deserialize(byte[] smartContractBytes)
        {
            try
            {
                var type = smartContractBytes[0];

                RLPCollection list = RLP.Decode(smartContractBytes.Skip(1).ToArray());

                RLPCollection innerList = (RLPCollection)list[0];

                IList<byte[]> encodedParamBytes = innerList.Select(x => x.RLPData).ToList();

                var vmVersion = this.primitiveSerializer.Deserialize<int>(encodedParamBytes[0]);
                var gasPrice = this.primitiveSerializer.Deserialize<ulong>(encodedParamBytes[1]);
                var gasLimit = (Gas) this.primitiveSerializer.Deserialize<ulong>(encodedParamBytes[2]);

                if (IsCallContract(type))
                {
                    var contractAddressBytes = this.primitiveSerializer.Deserialize<byte[]>(encodedParamBytes[3]);
                    var contractAddress = new uint160(contractAddressBytes);
                    var methodName = this.primitiveSerializer.Deserialize<string>(encodedParamBytes[4]);
                    var methodParameters = this.DeserializeMethodParameters(encodedParamBytes[5]);
                    var callData = new ContractTxData(vmVersion, gasPrice, gasLimit, contractAddress, methodName, methodParameters);
                    return Result.Ok(callData);
                }

                if (IsCreateContract(type))
                {
                    var contractExecutionCode = this.primitiveSerializer.Deserialize<byte[]>(encodedParamBytes[3]);
                    var methodParameters = this.DeserializeMethodParameters(encodedParamBytes[4]);

                    var callData = new ContractTxData(vmVersion, gasPrice, gasLimit, contractExecutionCode, methodParameters);
                    return Result.Ok(callData);
                }
            }
            catch (Exception e)
            {
                // TODO: Avoid this catch all exceptions
                return Result.Fail<ContractTxData>("Error deserializing calldata. " + e.Message);
            }

            return Result.Fail<ContractTxData>("Error deserializing calldata. Incorrect first byte.");
        }

        public byte[] Serialize(ContractTxData contractTxData)
        {
            var bytes = new List<byte[]>();

            bytes.Add(this.primitiveSerializer.Serialize(contractTxData.VmVersion));
            bytes.Add(this.primitiveSerializer.Serialize(contractTxData.GasPrice));
            bytes.Add(this.primitiveSerializer.Serialize((ulong)contractTxData.GasLimit));

            if (contractTxData.OpCodeType == (byte)ScOpcodeType.OP_CALLCONTRACT)
            {
                bytes.Add(contractTxData.ContractAddress.ToBytes());
                bytes.Add(this.primitiveSerializer.Serialize(contractTxData.MethodName));
            }

            if (contractTxData.OpCodeType == (byte)ScOpcodeType.OP_CREATECONTRACT)
                bytes.Add(contractTxData.ContractExecutionCode);

            if (contractTxData.MethodParameters != null && contractTxData.MethodParameters.Any())
                bytes.Add(this.methodParamSerializer.Serialize(contractTxData.MethodParameters));
            else
                bytes.Add(new byte[0]);

            var encoded = RLP.EncodeList(bytes.Select(RLP.EncodeElement).ToArray());
            var result = new byte[encoded.Length + 1];

            // Append the opcode to the start of the encoded list
            result[0] = contractTxData.OpCodeType;
            encoded.CopyTo(result, 1);
            return result;
        }

        private static bool IsCreateContract(byte type)
        {
            return type == (byte)ScOpcodeType.OP_CREATECONTRACT;
        }

        private static bool IsCallContract(byte type)
        {
            return type == (byte)ScOpcodeType.OP_CALLCONTRACT;
        }

        private object[] DeserializeMethodParameters(byte[] methodParametersRaw)
        {
            object[] methodParameters = null;

            if (methodParametersRaw != null && methodParametersRaw.Length > 0)
                methodParameters = this.methodParamSerializer.Deserialize(methodParametersRaw);
            return methodParameters;
        }
    }
}