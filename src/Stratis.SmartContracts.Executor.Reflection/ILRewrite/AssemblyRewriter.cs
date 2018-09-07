using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using RuntimeObserver;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{
    public class AssemblyRewriter
    {
        //public static Guid Rewrite(Stream source, Stream target)
        //{
        //    if (target == source) // For Cecil.
        //        throw new ArgumentException("Target stream must be different from source stream.", nameof(target));

        //    var assembly = AssemblyDefinition.ReadAssembly(source);
        //    Guid token = Rewrite(assembly);
        //    assembly.Write(target);
        //    return token;
        //}

        //// Does whole assembly but we probably only want to do module...

        //public static Guid Rewrite(AssemblyDefinition assembly)
        //{
        //    var id = Guid.NewGuid();
        //    foreach (ModuleDefinition module in assembly.Modules)
        //    {
        //        RewriteModule(module, id);
        //    }
        //    return id;
        //}

        // May be able to remove 'id' here

        public static Guid RewriteModule(ModuleDefinition module)
        {
            Guid id = Guid.NewGuid();

            FieldDefinition observerInstanceField = GetObserverInstance(module, id);
            var observer = new ObserverReferences(observerInstanceField, module);

            foreach (TypeDefinition type in module.Types)
            {
                RewriteType(type, observer);
            }

            return id;
        }

        private static FieldDefinition GetObserverInstance(ModuleDefinition module, Guid id)
        {
            var instanceType = new TypeDefinition(
                "<Stratis>", "<RuntimeObserverInstance>",
                TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.NotPublic,
                module.ImportReference(typeof(object))
            );
            var instanceField = new FieldDefinition(
                "Instance",
                FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly,
                module.ImportReference(typeof(Observer))
            );
            instanceType.Fields.Add(instanceField);

            var constructor = new MethodDefinition(
                ".cctor", MethodAttributes.Private | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName | MethodAttributes.Static,
                module.ImportReference(typeof(void))
            );
            MethodReference getGuardInstance = module.ImportReference(typeof(ObserverInstances).GetMethod(nameof(ObserverInstances.Get)));
            ILProcessor il = constructor.Body.GetILProcessor();
            il.Emit(OpCodes.Ldstr, id.ToString());
            il.Emit(OpCodes.Call, getGuardInstance);
            il.Emit(OpCodes.Stsfld, instanceField);
            il.Emit(OpCodes.Ret);
            instanceType.Methods.Add(constructor);

            module.Types.Add(instanceType);
            return instanceField;
        }

        private static void RewriteType(TypeDefinition type, ObserverReferences observer)
        {
            foreach (MethodDefinition method in type.Methods)
            {
                RewriteMethod(method, observer);
            }
        }

        private static void RewriteMethod(MethodDefinition method, ObserverReferences observer)
        {
            if (method.DeclaringType == observer.InstanceField.DeclaringType)
                return;

            if (!method.HasBody)
                return;

            if (method.Body.Instructions.Count == 0)
                return; // weird, but happens with 'extern'

            ILProcessor il = method.Body.GetILProcessor();
            var guardVariable = new VariableDefinition(observer.InstanceField.FieldType);
            il.Body.Variables.Add(guardVariable);

            Mono.Collections.Generic.Collection<Instruction> instructions = il.Body.Instructions;
            Instruction start = instructions[0];
            var skipFirst = 2;
            il.InsertBefore(start, il.Create(OpCodes.Ldsfld, observer.InstanceField));
            il.InsertBefore(start, il.CreateStlocBest(guardVariable));

            for (var i = skipFirst; i < instructions.Count; i++)
            {
                Instruction instruction = instructions[i];

                if (!ShouldInsertJumpGuardBefore(instruction))
                    continue;

                il.InsertBefore(instruction, il.CreateLdlocBest(guardVariable));
                il.InsertBefore(instruction, il.Create(OpCodes.Call, observer.OperationUpMethod));
                i += 2;
            }

            il.CorrectAllAfterChanges();
        }

        private static bool ShouldInsertJumpGuardBefore(Instruction instruction, bool ignorePrefix = false)
        {
            var opCode = instruction.OpCode;
            if (opCode.OpCodeType == OpCodeType.Prefix)
                return ShouldInsertJumpGuardBefore(instruction.Next, ignorePrefix: true);

            if (!ignorePrefix && instruction.Previous?.OpCode.OpCodeType == OpCodeType.Prefix)
                return false;

            var flowControl = opCode.FlowControl;
            if (flowControl == FlowControl.Next || flowControl == FlowControl.Return)
                return false;

            if (instruction.Operand is Instruction target && target.Offset > instruction.Offset)
                return false;

            return true;
        }

    }

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

        public static void CorrectAllAfterChanges(this ILProcessor il)
        {
            CorrectBranchSizes(il);
        }

        private static bool IsSByte(int value)
        {
            return value >= sbyte.MinValue && value <= sbyte.MaxValue;
        }

        private static void CorrectBranchSizes(ILProcessor il)
        {
            var offset = 0;
            foreach (var instruction in il.Body.Instructions)
            {
                offset += instruction.GetSize();
                instruction.Offset = offset;
            }

            foreach (var instruction in il.Body.Instructions)
            {
                var opCode = instruction.OpCode;
                if (opCode.OperandType != OperandType.ShortInlineBrTarget)
                    continue;

                var operandValue = ((Instruction)instruction.Operand).Offset - (instruction.Offset + instruction.GetSize());
                if (operandValue >= sbyte.MinValue && operandValue <= sbyte.MaxValue)
                    continue;

                instruction.OpCode = ConvertFromShortBranchOpCode(opCode);
            }
        }

        private static OpCode ConvertFromShortBranchOpCode(OpCode opCode)
        {
            switch (opCode.Code)
            {
                case Code.Br_S: return OpCodes.Br;
                case Code.Brfalse_S: return OpCodes.Brfalse;
                case Code.Brtrue_S: return OpCodes.Brtrue;
                case Code.Beq_S: return OpCodes.Beq;
                case Code.Bge_S: return OpCodes.Bge;
                case Code.Bge_Un_S: return OpCodes.Bge_Un;
                case Code.Bgt_S: return OpCodes.Bgt;
                case Code.Bgt_Un_S: return OpCodes.Bgt_Un;
                case Code.Ble_S: return OpCodes.Ble;
                case Code.Ble_Un_S: return OpCodes.Ble_Un;
                case Code.Blt_S: return OpCodes.Blt;
                case Code.Blt_Un_S: return OpCodes.Blt_Un;
                case Code.Bne_Un_S: return OpCodes.Bne_Un;
                case Code.Leave_S: return OpCodes.Leave;
                default:
                    throw new ArgumentOutOfRangeException("Unknown branch opcode: " + opCode);
            }
        }
    }
}
