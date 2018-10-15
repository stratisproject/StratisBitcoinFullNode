using System;
using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using NBitcoin;
using Nethereum.RLP;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class CallDataSerializer : ICallDataSerializer
    {
        public const int OpcodeSize = sizeof(byte);
        public const int VmVersionSize = sizeof(int);
        public const int GasPriceSize = sizeof(ulong);
        public const int GasLimitSize = sizeof(ulong);
        public const int AddressSize = 20;
        public const int PrefixSize = OpcodeSize + VmVersionSize + GasPriceSize + GasLimitSize;
        public const int CallContractPrefixSize = PrefixSize + AddressSize;

        private readonly IMethodParameterSerializer methodParamSerializer;
        private readonly IContractPrimitiveSerializer primitiveSerializer;

        public CallDataSerializer(IContractPrimitiveSerializer primitiveSerializer)
        {            
            this.primitiveSerializer = primitiveSerializer;
            this.methodParamSerializer = new MethodParameterByteSerializer(primitiveSerializer);
        }

        public Result<ContractTxData> Deserialize(byte[] smartContractBytes)
        {
            try
            {
                var type = smartContractBytes[0];
                var vmVersionBytes = smartContractBytes.Slice(OpcodeSize, VmVersionSize);
                var gasPriceBytes = smartContractBytes.Slice(OpcodeSize + VmVersionSize, GasPriceSize);
                var gasLimitBytes = smartContractBytes.Slice(OpcodeSize + VmVersionSize + GasPriceSize, GasLimitSize);                
                
                var vmVersion = this.primitiveSerializer.Deserialize<int>(vmVersionBytes);
                var gasPrice = this.primitiveSerializer.Deserialize<ulong>(gasPriceBytes);
                var gasLimit = (Gas) this.primitiveSerializer.Deserialize<ulong>(gasLimitBytes);

                if (IsCallContract(type))
                {
                    var contractAddressBytes = smartContractBytes.Slice(PrefixSize, AddressSize);
                    var contractAddress = new uint160(contractAddressBytes);

                    var remaining = smartContractBytes.Slice(CallContractPrefixSize, (uint)(smartContractBytes.Length - CallContractPrefixSize));

                    IList<byte[]> decodedParams = RLPDecode(remaining);

                    var methodName = this.primitiveSerializer.Deserialize<string>(decodedParams[0]);
                    var methodParameters = this.DeserializeMethodParameters(decodedParams[1]);
                    var callData = new ContractTxData(vmVersion, gasPrice, gasLimit, contractAddress, methodName, methodParameters);
                    return Result.Ok(callData);
                }

                if (IsCreateContract(type))
                {
                    var remaining = smartContractBytes.Slice(PrefixSize, (uint)(smartContractBytes.Length - PrefixSize));

                    IList<byte[]> decodedParams = RLPDecode(remaining);

                    var contractExecutionCode = this.primitiveSerializer.Deserialize<byte[]>(decodedParams[0]);
                    var methodParameters = this.DeserializeMethodParameters(decodedParams[1]);

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

        private static IList<byte[]> RLPDecode(byte[] remaining)
        {
            RLPCollection list = RLP.Decode(remaining);

            RLPCollection innerList = (RLPCollection) list[0];

            return innerList.Select(x => x.RLPData).ToList();
        }

        public byte[] Serialize(ContractTxData contractTxData)
        {
            byte opcode = contractTxData.OpCodeType;

            var rlpBytes = new List<byte[]>();

            if (IsCallContract(opcode))
            {
                rlpBytes.Add(this.primitiveSerializer.Serialize(contractTxData.MethodName));
            }

            if (IsCreateContract(opcode))
            {
                rlpBytes.Add(contractTxData.ContractExecutionCode);
            }

            if (contractTxData.MethodParameters != null && contractTxData.MethodParameters.Any())
            {
                rlpBytes.Add(this.SerializeMethodParameters(contractTxData.MethodParameters));
            }
            else
            {
                rlpBytes.Add(new byte[0]);
            }

            var encoded = RLP.EncodeList(rlpBytes.Select(RLP.EncodeElement).ToArray());

            byte[] vmVersion = this.primitiveSerializer.Serialize(contractTxData.VmVersion);
            byte[] gasPrice = this.primitiveSerializer.Serialize(contractTxData.GasPrice);
            byte[] gasLimit = this.primitiveSerializer.Serialize(contractTxData.GasLimit.Value);

            var bytes = IsCallContract(opcode) ? new byte[CallContractPrefixSize + encoded.Length] : new byte[PrefixSize + encoded.Length];

            bytes[0] = opcode;
            vmVersion.CopyTo(bytes, OpcodeSize);
            gasPrice.CopyTo(bytes, OpcodeSize + VmVersionSize);
            gasLimit.CopyTo(bytes, OpcodeSize + VmVersionSize + GasPriceSize);

            if (IsCallContract(opcode))
            {
                contractTxData.ContractAddress.ToBytes().CopyTo(bytes, PrefixSize);
                encoded.CopyTo(bytes, CallContractPrefixSize);
            }
            else
            {
                encoded.CopyTo(bytes, PrefixSize);
            }

            return bytes;
        }

        private static bool IsCreateContract(byte type)
        {
            return type == (byte)ScOpcodeType.OP_CREATECONTRACT;
        }

        private static bool IsCallContract(byte type)
        {
            return type == (byte)ScOpcodeType.OP_CALLCONTRACT;
        }

        private byte[] SerializeMethodParameters(object[] objects)
        {
            return this.methodParamSerializer.Serialize(objects);
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