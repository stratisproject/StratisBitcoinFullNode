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
            return IsCallContract(contractTxData.OpCodeType) 
                ? this.SerializeCallContract(contractTxData) 
                : this.SerializeCreateContract(contractTxData);
        }

        private byte[] SerializeCreateContract(ContractTxData contractTxData)
        {
            var rlpBytes = new List<byte[]>();

            rlpBytes.Add(contractTxData.ContractExecutionCode);
            
            this.AddMethodParams(rlpBytes, contractTxData.MethodParameters);
            
            var encoded = RLP.EncodeList(rlpBytes.Select(RLP.EncodeElement).ToArray());
            
            var bytes = new byte[PrefixSize + encoded.Length];
            bytes[0] = (byte)ScOpcodeType.OP_CREATECONTRACT;

            this.SerializePrefix(bytes, contractTxData);
            
            encoded.CopyTo(bytes, PrefixSize);

            return bytes;
        }

        private byte[] SerializeCallContract(ContractTxData contractTxData)
        {
            var rlpBytes = new List<byte[]>();

            rlpBytes.Add(this.primitiveSerializer.Serialize(contractTxData.MethodName));

            this.AddMethodParams(rlpBytes, contractTxData.MethodParameters);

            var encoded = RLP.EncodeList(rlpBytes.Select(RLP.EncodeElement).ToArray());
            
            var bytes = new byte[CallContractPrefixSize + encoded.Length];
            bytes[0] = (byte)ScOpcodeType.OP_CALLCONTRACT;
            this.SerializePrefix(bytes, contractTxData);

            contractTxData.ContractAddress.ToBytes().CopyTo(bytes, PrefixSize);
            encoded.CopyTo(bytes, CallContractPrefixSize);

            return bytes;
        }

        private void SerializePrefix(byte[] bytes, ContractTxData contractTxData)
        {
            byte[] vmVersion = this.primitiveSerializer.Serialize(contractTxData.VmVersion);
            byte[] gasPrice = this.primitiveSerializer.Serialize(contractTxData.GasPrice);
            byte[] gasLimit = this.primitiveSerializer.Serialize(contractTxData.GasLimit.Value);

            vmVersion.CopyTo(bytes, OpcodeSize);
            gasPrice.CopyTo(bytes, OpcodeSize + VmVersionSize);
            gasLimit.CopyTo(bytes, OpcodeSize + VmVersionSize + GasPriceSize);
        }

        private void AddMethodParams(List<byte[]> rlpBytes, object[] methodParameters)
        {
            if (methodParameters != null && methodParameters.Any())
            {
                rlpBytes.Add(this.SerializeMethodParameters(methodParameters));
            }
            else
            {
                rlpBytes.Add(new byte[0]);
            }
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