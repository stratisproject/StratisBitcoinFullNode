using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.Backend
{
    /// <summary>
    /// Creates gas spend operations with the correct costs
    /// </summary>
    public static class GasPriceList
    {
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
                    cost = (Gas)1;
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
            Gas cost = (Gas)(ulong)(20000 * keyBytes.Length + 20000 * valueBytes.Length);
            return cost;
        }

        /// <summary>
        /// TODO - Add actual costs
        /// </summary>
        /// <param name="methodToCall"></param>
        /// <returns></returns>
        public static Gas MethodCallCost(MethodReference methodToCall)
        {
            return (Gas) 0;
        }
    }
}