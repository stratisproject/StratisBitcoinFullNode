using System;
using System.Linq;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.Core.Serialization;

namespace Stratis.SmartContracts.Core
{
    public class SmartContractCarrierSerializer : ISmartContractCarrierSerializer
    {
        private readonly IMethodParameterSerializer methodParameterSerializer;
        private const int IntLength = sizeof(int);

        public SmartContractCarrierSerializer(IMethodParameterSerializer methodParameterSerializer)
        {
            this.methodParameterSerializer = methodParameterSerializer;
        }


        /// <summary> 
        /// Deserializes the smart contract execution code and other related information.
        /// </summary>
        public ISmartContractCarrier Deserialize(Transaction transaction)
        {
            TxOut smartContractTxOut = transaction.Outputs.FirstOrDefault(x => x.ScriptPubKey.IsSmartContractExec);
            byte[] smartContractBytes = smartContractTxOut.ScriptPubKey.ToBytes();
            byte opcode = smartContractBytes[0];
            var byteCursor = 0;
            var takeLength = 0;
            smartContractBytes = smartContractBytes.Skip(1).ToArray();

            var carrier = new SmartContractCarrier(new MethodParameterSerializer())
            {
                OpCodeType = (OpcodeType)opcode,
                VmVersion = Deserialize<int>(smartContractBytes, ref byteCursor, ref takeLength)
            };

            if (carrier.OpCodeType == OpcodeType.OP_CALLCONTRACT)
            {
                carrier.ContractAddress = Deserialize<uint160>(smartContractBytes, ref byteCursor, ref takeLength);
                carrier.MethodName = Deserialize<string>(smartContractBytes, ref byteCursor, ref takeLength);
            }

            if (carrier.OpCodeType == OpcodeType.OP_CREATECONTRACT)
                carrier.ContractExecutionCode = Deserialize<byte[]>(smartContractBytes, ref byteCursor, ref takeLength);

            var methodParameters = Deserialize<string>(smartContractBytes, ref byteCursor, ref takeLength);
            if (!string.IsNullOrEmpty(methodParameters))
                carrier.MethodParameters = this.methodParameterSerializer.ToObjects(methodParameters);

            carrier.Nvout = Convert.ToUInt32(transaction.Outputs.IndexOf(smartContractTxOut));
            carrier.GasPrice = (Gas)Deserialize<ulong>(smartContractBytes, ref byteCursor, ref takeLength);
            carrier.GasLimit = (Gas)Deserialize<ulong>(smartContractBytes, ref byteCursor, ref takeLength);
            carrier.TransactionHash = transaction.GetHash();
            carrier.Value = smartContractTxOut.Value;

            return carrier;
        }

        private static T Deserialize<T>(byte[] smartContractBytes, ref int byteCursor, ref int takeLength)
        {
            takeLength = BitConverter.ToInt16(smartContractBytes.Skip(byteCursor).Take(IntLength).ToArray(), 0);
            byteCursor += IntLength;

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
