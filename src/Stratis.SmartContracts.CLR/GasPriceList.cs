using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

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

        public const uint StoragePerByteSavedGasCost = 20;
        public const uint StoragePerByteRetrievedGasCost = 1;
        public const uint LogPerTopicByteCost = 2;
        public const uint LogPerByteCost = 1;
        public const uint MethodCallGasCost = 5;
        public const uint InstructionGasCost = 1;
        public const long StorageCheckContractExistsCost = 5;

        /// <summary>
        /// Get the gas cost for a specific instruction. For v1 all instructions are priced equally.
        /// </summary>
        public static ulong InstructionOperationCost(Instruction instruction)
        {
            OpCode opcode = instruction.OpCode;
            ulong cost;

            switch (opcode.Name)
            {
                default:
                    cost = InstructionGasCost;
                    break;
            }

            return cost;
        }

        /// <summary>
        /// Gas cost to log an event inside a contract.
        /// </summary>
        public static ulong LogOperationCost(IEnumerable<byte[]> topics, byte[] data)
        {
            long topicCost = topics.Select(x => x.Length * LogPerTopicByteCost).Sum();
            long dataCost = data.Length * LogPerByteCost;
            return (ulong) (topicCost + dataCost);
        }

        /// <summary>
        /// Get cost to store this key and value.
        /// </summary>
        public static ulong StorageSaveOperationCost(byte[] keyBytes, byte[] valueBytes)
        {
            int keyLen = keyBytes != null ? keyBytes.Length : 0;
            int valueLen = valueBytes != null ? valueBytes.Length : 0;
            return (ulong)(StoragePerByteSavedGasCost * keyLen + StoragePerByteSavedGasCost * valueLen);
        }

        /// <summary>
        /// Get cost to retrieve this value via key.
        /// </summary>
        public static ulong StorageRetrieveOperationCost(byte[] keyBytes, byte[] valueBytes)
        {
            int keyLen = keyBytes != null ? keyBytes.Length : 0;
            int valueLen = valueBytes != null ? valueBytes.Length : 0;
            return (ulong)(StoragePerByteRetrievedGasCost * keyLen + StoragePerByteRetrievedGasCost * valueLen);
        }

        /// <summary>
        /// TODO - Add actual costs
        /// </summary>
        public static ulong MethodCallCost(MethodReference methodToCall)
        {
            return MethodCallGasCost;
        }
    }
}