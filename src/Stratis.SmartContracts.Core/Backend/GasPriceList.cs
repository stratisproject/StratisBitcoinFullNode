using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.Core.Backend
{
    /// <summary>
    /// Creates gas spend operations with the correct costs
    /// </summary>
    public static class GasPriceList
    {
        private const int StorageGasCost = 1;
        private const int MethodCallGasCost = 0;
        private const int InstructionGasCost = 1;

        /// <summary>
        /// TODO - Add actual costs
        /// </summary>
        /// <param name="instruction"></param>
        /// <returns></returns>
        public static Gas InstructionOperationCost(Instruction instruction)
        {
            OpCode opcode = instruction.OpCode;
            Gas cost;

            switch (opcode.Name)
            {
                default:
                    cost = (Gas) InstructionGasCost;
                    break;
            }

            return cost;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keyBytes"></param>
        /// <param name="valueBytes"></param>
        /// <returns></returns>
        public static Gas StorageOperationCost(byte[] keyBytes, byte[] valueBytes)
        {
            Gas cost = (Gas)(ulong)(StorageGasCost * keyBytes.Length + StorageGasCost * valueBytes.Length);
            return cost;
        }

        /// <summary>
        /// TODO - Add actual costs
        /// </summary>
        /// <param name="methodToCall"></param>
        /// <returns></returns>
        public static Gas MethodCallCost(MethodReference methodToCall)
        {
            return (Gas) MethodCallGasCost;
        }
    }
}