using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Creates gas spend operations with the correct costs
    /// </summary>
    public static class GasPriceList
    {
        /// <summary>The base cost trying to execute a smart contract.</summary>
        public const ulong BaseCost = 1000;

        /// <summary>The cost per gas unit if contract does not exist.</summary>
        public const ulong ContractDoesNotExistCost = 1000;

        /// <summary>The cost per gas unit if contract validation fails.</summary>
        public const ulong ContractValidationFailedCost = 1000;

        public const int StoragePerByteSavedGasCost = 10;
        public const int StoragePerByteRetrievedGasCost = 1;
        public const int MethodCallGasCost = 5;
        public const int InstructionGasCost = 1;

        public static Gas ContractDoesNotExist()
        {
            return (Gas)(ContractDoesNotExistCost);
        }

        public static Gas ContractValidationFailed()
        {
            return (Gas)(ContractValidationFailedCost);
        }

        /// <summary>
        /// TODO - Add actual costs
        /// </summary>
        /// <param name="instruction"></param>
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
        /// Get cost to store this key and value.
        /// </summary>
        public static Gas StorageSaveOperationCost(byte[] keyBytes, byte[] valueBytes)
        {
            Gas cost = (Gas)(ulong)(StoragePerByteSavedGasCost * keyBytes.Length + StoragePerByteSavedGasCost * valueBytes.Length);
            return cost;
        }

        /// <summary>
        /// Get cost to retrieve this value via key.
        /// </summary>
        public static Gas StorageRetrieveOperationCost(byte[] keyBytes, byte[] valueBytes)
        {
            Gas cost = (Gas)(ulong)(StoragePerByteRetrievedGasCost * keyBytes.Length + StoragePerByteRetrievedGasCost * valueBytes.Length);
            return cost;
        }

        /// <summary>
        /// TODO - Add actual costs
        /// </summary>
        /// <param name="methodToCall"></param>
        public static Gas MethodCallCost(MethodReference methodToCall)
        {
            return (Gas)MethodCallGasCost;
        }
    }
}