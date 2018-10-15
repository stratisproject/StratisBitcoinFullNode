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
                var spareByte = new byte[sizeof(int)];
                var gasPriceBytes = new byte[sizeof(ulong)];
                var gasLimitBytes = new byte[sizeof(ulong)];
                var prefixLength = sizeof(byte) + spareByte.Length + gasPriceBytes.Length + gasLimitBytes.Length;
                var remaining = new byte[smartContractBytes.Length - prefixLength];

                Array.Copy(smartContractBytes, sizeof(byte), spareByte, 0, 1);
                Array.Copy(smartContractBytes, sizeof(byte) + spareByte.Length, gasPriceBytes, 0, gasPriceBytes.Length);
                Array.Copy(smartContractBytes, sizeof(byte) + spareByte.Length + gasPriceBytes.Length, gasLimitBytes, 0, gasLimitBytes.Length);
                Array.Copy(smartContractBytes, prefixLength, remaining, 0, remaining.Length);

                var vmVersion = this.primitiveSerializer.Deserialize<int>(spareByte);
                var gasPrice = this.primitiveSerializer.Deserialize<ulong>(gasPriceBytes);
                var gasLimit = (Gas) this.primitiveSerializer.Deserialize<ulong>(gasLimitBytes);
                
                RLPCollection list = RLP.Decode(remaining);

                RLPCollection innerList = (RLPCollection)list[0];

                IList<byte[]> encodedParamBytes = innerList.Select(x => x.RLPData).ToList();

                if (IsCallContract(type))
                {
                    var contractAddressBytes = this.primitiveSerializer.Deserialize<byte[]>(encodedParamBytes[0]);
                    var contractAddress = new uint160(contractAddressBytes);
                    var methodName = this.primitiveSerializer.Deserialize<string>(encodedParamBytes[1]);
                    var methodParameters = this.DeserializeMethodParameters(encodedParamBytes[2]);
                    var callData = new ContractTxData(vmVersion, gasPrice, gasLimit, contractAddress, methodName, methodParameters);
                    return Result.Ok(callData);
                }

                if (IsCreateContract(type))
                {
                    var contractExecutionCode = this.primitiveSerializer.Deserialize<byte[]>(encodedParamBytes[0]);
                    var methodParameters = this.DeserializeMethodParameters(encodedParamBytes[1]);

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

            bytes.Add(new [] {contractTxData.OpCodeType});
            bytes.Add(this.primitiveSerializer.Serialize(contractTxData.VmVersion));
            bytes.Add(this.primitiveSerializer.Serialize(contractTxData.GasPrice));
            bytes.Add(this.primitiveSerializer.Serialize((ulong)contractTxData.GasLimit));

            var rlpBytes = new List<byte[]>();
            if (contractTxData.OpCodeType == (byte)ScOpcodeType.OP_CALLCONTRACT)
            {
                rlpBytes.Add(contractTxData.ContractAddress.ToBytes());
                rlpBytes.Add(this.primitiveSerializer.Serialize(contractTxData.MethodName));
            }

            if (contractTxData.OpCodeType == (byte)ScOpcodeType.OP_CREATECONTRACT)
                rlpBytes.Add(contractTxData.ContractExecutionCode);

            if (contractTxData.MethodParameters != null && contractTxData.MethodParameters.Any())
                rlpBytes.Add(this.methodParamSerializer.Serialize(contractTxData.MethodParameters));
            else
                rlpBytes.Add(new byte[0]);

            var encoded = RLP.EncodeList(rlpBytes.Select(RLP.EncodeElement).ToArray());

            bytes.Add(encoded);
            return bytes.SelectMany(b => b).ToArray();
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