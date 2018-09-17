using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{
    public static class CecilExtensions
    {
        /// <summary>
        /// Get the simplest instruction for popping item on stack into a variable index.
        /// </summary>
        public static Instruction CreateStlocBest(this ILProcessor il, VariableDefinition variable)
        {
            switch (variable.Index)
            {
                case 0: return il.Create(OpCodes.Stloc_0);
                case 1: return il.Create(OpCodes.Stloc_1);
                case 2: return il.Create(OpCodes.Stloc_2);
                case 3: return il.Create(OpCodes.Stloc_3);
                default:
                    if (IsSByte(variable.Index))
                        return il.Create(OpCodes.Stloc_S, variable);
                    return il.Create(OpCodes.Stloc, variable);
            }
        }

        /// <summary>
        /// Get the simplest instruction for loading an item onto the stack from a variable.
        /// </summary>
        public static Instruction CreateLdlocBest(this ILProcessor il, VariableDefinition variable)
        {
            switch (variable.Index)
            {
                case 0: return il.Create(OpCodes.Ldloc_0);
                case 1: return il.Create(OpCodes.Ldloc_1);
                case 2: return il.Create(OpCodes.Ldloc_2);
                case 3: return il.Create(OpCodes.Ldloc_3);
                default:
                    if (IsSByte(variable.Index))
                        return il.Create(OpCodes.Ldloc_S, variable);
                    return il.Create(OpCodes.Ldloc, variable);
            }
        }

        /// <summary>
        /// Helper method for inserting several instructions after a target instruction in their given order.
        /// </summary>
        public static void InsertAfter(this ILProcessor il, Instruction target, params Instruction[] instructions)
        {
            int position = 0;
            Instruction lastAdded = target;
            while (position < instructions.Length)
            {
                il.InsertAfter(lastAdded, instructions[position]);
                lastAdded = instructions[position];
                position++;
            }
        }

        /// <summary>
        /// Whether the given integer is within the boundaries of a signed byte.
        /// </summary>
        private static bool IsSByte(int value)
        {
            return value >= sbyte.MinValue && value <= sbyte.MaxValue;
        }
    }
}
