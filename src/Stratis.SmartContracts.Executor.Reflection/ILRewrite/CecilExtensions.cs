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

        private static bool IsSByte(int value)
        {
            return value >= sbyte.MinValue && value <= sbyte.MaxValue;
        }
    }
}
