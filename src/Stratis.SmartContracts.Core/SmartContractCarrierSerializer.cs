using System.Linq;
using NBitcoin;
using Stratis.SmartContracts.Core.Serialization;

namespace Stratis.SmartContracts.Core
{
    public class SmartContractCarrierSerializer : ISmartContractCarrierSerializer
    {
        public ISmartContractCarrier Deserialize(Transaction transaction)
        {
            TxOut smartContractTxOut = transaction.Outputs.FirstOrDefault(x => x.ScriptPubKey.IsSmartContractExec);
            byte[] smartContractBytes = smartContractTxOut.ScriptPubKey.ToBytes();

            var byteCursor = 0;
            var takeLength = 0;

            var carrier = new SmartContractCarrier(new MethodParameterSerializer())
            {
                VmVersion = Deserialize<int>(smartContractBytes, ref byteCursor, ref takeLength),
                OpCodeType = (OpcodeType)Deserialize<short>(smartContractBytes, ref byteCursor, ref takeLength)
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
                carrier.MethodParameters = carrier.serializer.ToObjects(methodParameters);

            carrier.Nvout = Convert.ToUInt32(transaction.Outputs.IndexOf(smartContractTxOut));
            carrier.GasPrice = (Gas)Deserialize<ulong>(smartContractBytes, ref byteCursor, ref takeLength);
            carrier.GasLimit = (Gas)Deserialize<ulong>(smartContractBytes, ref byteCursor, ref takeLength);
            carrier.TransactionHash = transaction.GetHash();
            carrier.TxOutValue = smartContractTxOut.Value;

            return carrier;
        }
    }
}
