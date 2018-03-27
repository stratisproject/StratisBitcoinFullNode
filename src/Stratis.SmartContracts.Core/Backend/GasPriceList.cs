using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.Core.Backend
{
    /// <summary>
    /// Creates gas spend operations with the correct costs
    /// </summary>
    public static class GasPriceList
    {
        /// <summary>The cost per gas unit if contract does not exist.</summary>
        private const ulong ContractDoesNotExistCost = 1000;

        /// <summary>The cost per gas unit if contract validation fails.</summary>
        private const ulong ContractValidationFailedCost = 1000;

        private const int StorageGasCost = 1;
        private const int MethodCallGasCost = 0;
        private const int InstructionGasCost = 1;

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
        /// 
        /// </summary>
        /// <param name="keyBytes"></param>
        /// <param name="valueBytes"></param>
        public static Gas StorageOperationCost(byte[] keyBytes, byte[] valueBytes)
        {
            Gas cost = (Gas)(ulong)(StorageGasCost * keyBytes.Length + StorageGasCost * valueBytes.Length);
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