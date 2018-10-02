using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class CallDataSerializer : ICallDataSerializer
    {
        public IMethodParameterSerializer MethodParamSerializer { get; }

        private const int intLength = sizeof(int);

        public CallDataSerializer(IMethodParameterSerializer methodParameterSerializer)
        {
            this.MethodParamSerializer = methodParameterSerializer;
        }

        public Result<ContractTxData> Deserialize(byte[] smartContractBytes)
        {
            try
            {
                var byteCursor = 1;
                var takeLength = 0;

                var type = smartContractBytes[0];

                var vmVersion = Deserialize<int>(smartContractBytes, ref byteCursor, ref takeLength);
                var gasPrice = (Gas)Deserialize<ulong>(smartContractBytes, ref byteCursor, ref takeLength);
                var gasLimit = (Gas)Deserialize<ulong>(smartContractBytes, ref byteCursor, ref takeLength);

                if (IsCallContract(type))
                {
                    var contractAddress = Deserialize<uint160>(smartContractBytes, ref byteCursor, ref takeLength);
                    var methodName = Deserialize<string>(smartContractBytes, ref byteCursor, ref takeLength);
                    var methodParametersRaw = Deserialize<byte[]>(smartContractBytes, ref byteCursor, ref takeLength);

                    var methodParameters = this.DeserializeMethodParameters(methodParametersRaw);

                    var callData = new ContractTxData(vmVersion, gasPrice, gasLimit, contractAddress, methodName, methodParameters);
                    return Result.Ok(callData);
                }

                if (IsCreateContract(type))
                {
                    var contractExecutionCode = Deserialize<byte[]>(smartContractBytes, ref byteCursor, ref takeLength);
                    var methodParametersRaw = Deserialize<byte[]>(smartContractBytes, ref byteCursor, ref takeLength);

                    var methodParameters = this.DeserializeMethodParameters(methodParametersRaw);

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
            var bytes = new List<byte>
            {
                contractTxData.OpCodeType
            };

            bytes.AddRange(PrefixLength(BitConverter.GetBytes(contractTxData.VmVersion)));
            bytes.AddRange(PrefixLength(BitConverter.GetBytes(contractTxData.GasPrice)));
            bytes.AddRange(PrefixLength(BitConverter.GetBytes(contractTxData.GasLimit)));

            if (contractTxData.OpCodeType == (byte)ScOpcodeType.OP_CALLCONTRACT)
            {
                bytes.AddRange(PrefixLength(contractTxData.ContractAddress.ToBytes()));
                bytes.AddRange(PrefixLength(Encoding.UTF8.GetBytes(contractTxData.MethodName)));
            }

            if (contractTxData.OpCodeType == (byte)ScOpcodeType.OP_CREATECONTRACT)
                bytes.AddRange(PrefixLength(contractTxData.ContractExecutionCode));

            if (contractTxData.MethodParameters != null && contractTxData.MethodParameters.Any())
                bytes.AddRange(PrefixLength(this.MethodParamSerializer.Serialize(contractTxData.MethodParameters)));
            else
                bytes.AddRange(BitConverter.GetBytes(0));

            return bytes.ToArray();
        }

        /// <summary>
        /// Prefixes the byte array with the length of the array that follows.
        /// </summary>
        private static byte[] PrefixLength(byte[] toPrefix)
        {
            var prefixedBytes = new List<byte>();
            prefixedBytes.AddRange(BitConverter.GetBytes(toPrefix.Length));
            prefixedBytes.AddRange(toPrefix);
            return prefixedBytes.ToArray();
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
                methodParameters = this.MethodParamSerializer.Deserialize(methodParametersRaw);
            return methodParameters;
        }

        private static T Deserialize<T>(byte[] smartContractBytes, ref int byteCursor, ref int takeLength)
        {
            takeLength = BitConverter.ToInt16(smartContractBytes.Skip(byteCursor).Take(intLength).ToArray(), 0);
            byteCursor += intLength;

            if (takeLength == 0)
                return default(T);

            object result = null;

            if (typeof(T) == typeof(bool))
                result = BitConverter.ToBoolean(smartContractBytes.Skip(byteCursor).Take(takeLength).ToArray(), 0);

            if (typeof(T) == typeof(byte[]))
                result = smartContractBytes.Skip(byteCursor).Take(takeLength).ToArray();

            if (typeof(T) == typeof(int))
                result = BitConverter.ToInt32(smartContractBytes.Skip(byteCursor).Take(takeLength).ToArray(), 0);

            if (typeof(T) == typeof(short))
                result = BitConverter.ToInt16(smartContractBytes.Skip(byteCursor).Take(takeLength).ToArray(), 0);

            if (typeof(T) == typeof(string))
                result = Encoding.UTF8.GetString(smartContractBytes.Skip(byteCursor).Take(takeLength).ToArray());

            if (typeof(T) == typeof(uint))
                result = BitConverter.ToUInt32(smartContractBytes.Skip(byteCursor).Take(takeLength).ToArray(), 0);

            if (typeof(T) == typeof(uint160))
                result = new uint160(smartContractBytes.Skip(byteCursor).Take(takeLength).ToArray());

            if (typeof(T) == typeof(ulong))
                result = BitConverter.ToUInt64(smartContractBytes.Skip(byteCursor).Take(takeLength).ToArray(), 0);

            byteCursor += takeLength;

            return (T)result;
        }
    }
}