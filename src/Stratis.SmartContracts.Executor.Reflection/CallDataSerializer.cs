using System;
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
        // TODO this is ugly but there is poor DI support for rules so we can't inject it yet
        public static ICallDataSerializer Default = new CallDataSerializer(new MethodParameterSerializer());

        private readonly IMethodParameterSerializer methodParamSerializer;

        private const int intLength = sizeof(int);

        public CallDataSerializer(IMethodParameterSerializer methodParameterSerializer)
        {
            this.methodParamSerializer = methodParameterSerializer;
        }

        public Result<ContractTxData> Deserialize(byte[] smartContractBytes)
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
                var methodParametersRaw = Deserialize<string>(smartContractBytes, ref byteCursor, ref takeLength);

                var methodParameters = this.DeserializeMethodParameters(methodParametersRaw);

                var callData = new ContractTxData(vmVersion, gasPrice, gasLimit, contractAddress, methodName, methodParametersRaw, methodParameters);
                return Result.Ok(callData);
            }

            if (IsCreateContract(type))
            {
                var contractExecutionCode = Deserialize<byte[]>(smartContractBytes, ref byteCursor, ref takeLength);
                var methodParametersRaw = Deserialize<string>(smartContractBytes, ref byteCursor, ref takeLength);

                var methodParameters = this.DeserializeMethodParameters(methodParametersRaw);

                var callData = new ContractTxData(vmVersion, gasPrice, gasLimit, contractExecutionCode, methodParametersRaw, methodParameters);
                return Result.Ok(callData);
            }

            return Result.Fail<ContractTxData>("Error deserializing calldata");
        }

        private static bool IsCreateContract(byte type)
        {
            return type == (byte)ScOpcodeType.OP_CREATECONTRACT;
        }

        private static bool IsCallContract(byte type)
        {
            return type == (byte)ScOpcodeType.OP_CALLCONTRACT;
        }

        private object[] DeserializeMethodParameters(string methodParametersRaw)
        {
            object[] methodParameters = null;

            if (!string.IsNullOrWhiteSpace(methodParametersRaw))
                methodParameters = this.methodParamSerializer.ToObjects(methodParametersRaw);
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