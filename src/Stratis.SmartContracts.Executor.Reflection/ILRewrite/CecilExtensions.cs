using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{

    public static class CecilExtensions
    {
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

        private static bool IsSByte(int value)
        {
            return value >= sbyte.MinValue && value <= sbyte.MaxValue;
        }
    }
}
