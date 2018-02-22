using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.Backend
{
    /// <summary>
    /// Creates gas spend operations with the correct costs
    /// </summary>
    public static class GasPriceList
    {
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

        public static Gas StorageOperationCost(byte[] keyBytes, byte[] valueBytes)
        {
            Gas cost = (Gas)(ulong)(20000 * keyBytes.Length + 20000 * valueBytes.Length);
            return cost;
        }
    }
}