using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Creates gas spend operations with the correct costs
    /// </summary>
    public static class GasPriceList
    {
        /// <summary>The base cost trying to execute a smart contract.</summary>
        public const ulong BaseCost = 10_000;

        /// <summary>The cost to create and execute the constructor of a contract. To account for validation and code storage.</summary>
        public const ulong CreateCost = 12_000;

        /// <summary>The cost for transferring funds to a P2PKH from inside a contract.</summary>
        public const ulong TransferCost = 1_000;

        public const int StoragePerByteSavedGasCost = 20;
        public const int StoragePerByteRetrievedGasCost = 1;
        public const int LogPerTopicByteCost = 2;
        public const int LogPerByteCost = 1;
        public const int MethodCallGasCost = 5;
        public const int InstructionGasCost = 1;
        public const ulong StorageCheckContractExistsCost = 5;

        /// <summary>
        /// Get the gas cost for a specific instruction. For v1 all instructions are priced equally.
        /// </summary>
        public static Gas InstructionOperationCost(Instruction instruction)
        {
            OpCode opcode = instruction.OpCode;
            Gas cost;

            switch (opcode.Name)
            {
                default:
                    cost = (Gas)InstructionGasCost;
                    break;
            }

            return cost;
        }

        /// <summary>
        /// Gas cost to log an event inside a contract.
        /// </summary>
        public static Gas LogOperationCost(IEnumerable<byte[]> topics, byte[] data)
        {
            int topicCost = topics.Select(x => x.Length * LogPerTopicByteCost).Sum();
            int dataCost = data.Length * LogPerByteCost;
            return (Gas)(ulong) (topicCost + dataCost);
        }

        /// <summary>
        /// Get cost to store this key and value.
        /// </summary>
        public static Gas StorageSaveOperationCost(byte[] keyBytes, byte[] valueBytes)
        {
            int keyLen = keyBytes != null ? keyBytes.Length : 0;
            int valueLen = valueBytes != null ? valueBytes.Length : 0;

            var cost = (Gas)(ulong)(StoragePerByteSavedGasCost * keyLen + StoragePerByteSavedGasCost * valueLen);
            return cost;
        }

        /// <summary>
        /// Get cost to retrieve this value via key.
        /// </summary>
        public static Gas StorageRetrieveOperationCost(byte[] keyBytes, byte[] valueBytes)
        {
            int keyLen = keyBytes != null ? keyBytes.Length : 0;
            int valueLen = valueBytes != null ? valueBytes.Length : 0;

            var cost = (Gas)(ulong)(StoragePerByteRetrievedGasCost * keyLen + StoragePerByteRetrievedGasCost * valueLen);
            return cost;
        }

        /// <summary>
        /// TODO - Add actual costs
        /// </summary>
        public static Gas MethodCallCost(MethodReference methodToCall)
        {
            return (Gas)MethodCallGasCost;
        }
    }
}