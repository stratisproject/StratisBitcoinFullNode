using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using RuntimeObserver;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{
    public class ObserverRewriter : IILRewriter
    {
        private static readonly HashSet<OpCode> BranchingOps = new HashSet<OpCode>
        {
            OpCodes.Beq,
            OpCodes.Beq_S,
            OpCodes.Bge,
            OpCodes.Bge_S,
            OpCodes.Bge_Un,
            OpCodes.Bge_Un_S,
            OpCodes.Bgt,
            OpCodes.Bgt_S,
            OpCodes.Ble,
            OpCodes.Ble_S,
            OpCodes.Ble_Un,
            OpCodes.Blt,
            OpCodes.Bne_Un,
            OpCodes.Bne_Un_S,
            OpCodes.Br,
            OpCodes.Brfalse,
            OpCodes.Brfalse_S,
            OpCodes.Brtrue,
            OpCodes.Brtrue_S,
            OpCodes.Br_S
        };

        private static readonly HashSet<OpCode> CallingOps = new HashSet<OpCode>
        {
            OpCodes.Call,
            OpCodes.Calli,
            OpCodes.Callvirt
        };

        public Guid LastRewritten { get; private set; }

        public ModuleDefinition Rewrite(ModuleDefinition module)
        {
            Guid id = Guid.NewGuid();

            FieldDefinition observerInstanceField = GetObserverInstance(module, id);
            var observer = new ObserverReferences(observerInstanceField, module);

            foreach (TypeDefinition type in module.Types)
            {
                RewriteType(type, observer);
            }

            this.LastRewritten = id;

            return module;
        }

        private FieldDefinition GetObserverInstance(ModuleDefinition module, Guid id)
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

        private void RewriteType(TypeDefinition type, ObserverReferences observer)
        {
            foreach (MethodDefinition method in type.Methods)
            {
                RewriteMethod(method, observer);
            }
        }


        private void RewriteMethod(MethodDefinition methodDefinition, ObserverReferences observer)
        {
            if (methodDefinition.DeclaringType == observer.InstanceField.DeclaringType)
                return; // don't inject on our special instance.

            if (!methodDefinition.HasBody || methodDefinition.Body.Instructions.Count == 0)
                return; // don't inject on method without a Body 

            List<Instruction> branches = methodDefinition.Body.Instructions.Where(x => BranchingOps.Contains(x.OpCode)).ToList();
            List<Instruction> branchTos = branches.Select(x => (Instruction)x.Operand).ToList();

            Instruction currentSegmentStart = methodDefinition.Body.Instructions.FirstOrDefault();
            Gas gasTally = Gas.None;

            Dictionary<Instruction, Gas> gasToSpendForSegment = new Dictionary<Instruction, Gas>();

            // Inject observer instance to method.
            ILProcessor il = methodDefinition.Body.GetILProcessor();
            var observerVariable = new VariableDefinition(observer.InstanceField.FieldType);
            il.Body.Variables.Add(observerVariable);
            Instruction start = methodDefinition.Body.Instructions[0];
            il.InsertBefore(start, il.Create(OpCodes.Ldsfld, observer.InstanceField));
            il.InsertBefore(start, il.CreateStlocBest(observerVariable));

            // Start at 2 because of the instructions we just added. 
            int position = 2;

            while (position < methodDefinition.Body.Instructions.Count)
            {
                Instruction instruction = methodDefinition.Body.Instructions[position];

                Gas instructionCost = GasPriceList.InstructionOperationCost(instruction);

                // is the end of a segment. Include the current instruction in the count.
                if (branches.Contains(instruction))
                {
                    gasTally = (Gas)(gasTally + instructionCost);
                    gasToSpendForSegment.Add(currentSegmentStart, gasTally);
                    gasTally = Gas.None;
                    position++;
                    if (position == methodDefinition.Body.Instructions.Count)
                        break;
                    currentSegmentStart = methodDefinition.Body.Instructions[position];
                }
                // is the start of a new segment. Don't include the current instruction in count.
                else if (branchTos.Contains(instruction) && instruction != currentSegmentStart)
                {
                    gasToSpendForSegment.Add(currentSegmentStart, gasTally);
                    gasTally = Gas.None;
                    currentSegmentStart = instruction;
                    position++;
                }
                // is a call to another method
                else if (CallingOps.Contains(instruction.OpCode))
                {
                    var methodToCall = (MethodReference)instruction.Operand;

                    // If it's a method inside this contract then the gas will be injected no worries.
                    if (methodToCall.DeclaringType == methodDefinition.DeclaringType)
                    {
                        position++;
                        gasTally = (Gas)(gasTally + instructionCost);
                    }
                    // If it's a method outside this contract then we will need to get some average in future.
                    else
                    {
                        Gas methodCallCost = GasPriceList.MethodCallCost(methodToCall);

                        position++;
                        gasTally = (Gas)(gasTally + instructionCost + methodCallCost);
                    }
                }
                // any other instruction. just increase counter.
                else
                {
                    position++;
                    gasTally = (Gas)(gasTally + instructionCost);
                }
            }

            if (!gasToSpendForSegment.ContainsKey(currentSegmentStart))
                gasToSpendForSegment.Add(currentSegmentStart, gasTally);

            foreach (Instruction instruction in gasToSpendForSegment.Keys)
            {
                Instruction injectAfterInstruction = instruction;

                // If it's a constructor we need to skip the first 3 instructions. 
                // These will always be invoking the base constructor
                // ldarg.0
                // ldarg.0
                // call SmartContract::ctor
                if (methodDefinition.IsConstructor)
                {
                    injectAfterInstruction = instruction.Next.Next.Next;
                }

                AddSpendGasMethodBeforeInstruction(methodDefinition, observer, observerVariable, injectAfterInstruction, gasToSpendForSegment[instruction]);
            }

            foreach (Instruction instruction in branches)
            {
                var oldReference = (Instruction)instruction.Operand;
                Instruction newReference = oldReference.Previous.Previous.Previous; // 3 were inserted
                Instruction newInstruction = il.Create(instruction.OpCode, newReference);
                il.Replace(instruction, newInstruction);
            }
        }

        private static void AddSpendGasMethodBeforeInstruction(MethodDefinition methodDefinition, ObserverReferences observer, VariableDefinition variable, Instruction instruction, Gas opcodeCount)
        {
            ILProcessor il = methodDefinition.Body.GetILProcessor();
            il.InsertBefore(instruction, il.CreateLdlocBest(variable)); // load observer
            il.InsertBefore(instruction, il.Create(OpCodes.Ldc_I8, (long)opcodeCount.Value)); // load gas amount
            il.InsertBefore(instruction, il.Create(OpCodes.Call, observer.SpendGasMethod)); // trigger method
        }

        //private void RewriteMethod(MethodDefinition method, ObserverReferences observer)
        //{
        //    if (method.DeclaringType == observer.InstanceField.DeclaringType)
        //        return;

        //    if (!method.HasBody)
        //        return;

        //    if (method.Body.Instructions.Count == 0)
        //        return; // weird, but happens with 'extern'

        //    ILProcessor il = method.Body.GetILProcessor();
        //    var guardVariable = new VariableDefinition(observer.InstanceField.FieldType);
        //    il.Body.Variables.Add(guardVariable);

        //    Mono.Collections.Generic.Collection<Instruction> instructions = il.Body.Instructions;
        //    Instruction start = instructions[0];
        //    var skipFirst = 2;
        //    il.InsertBefore(start, il.Create(OpCodes.Ldsfld, observer.InstanceField));
        //    il.InsertBefore(start, il.CreateStlocBest(guardVariable));

        //    for (var i = skipFirst; i < instructions.Count; i++)
        //    {
        //        Instruction instruction = instructions[i];

        //        if (!ShouldInsertJumpGuardBefore(instruction))
        //            continue;

        //        il.InsertBefore(instruction, il.CreateLdlocBest(guardVariable));
        //        il.InsertBefore(instruction, il.Create(OpCodes.Call, observer.OperationUpMethod));
        //        i += 2;
        //    }

        //    il.CorrectAllAfterChanges();
        //}

        //private bool ShouldInsertJumpGuardBefore(Instruction instruction, bool ignorePrefix = false)
        //{
        //    var opCode = instruction.OpCode;
        //    if (opCode.OpCodeType == OpCodeType.Prefix)
        //        return ShouldInsertJumpGuardBefore(instruction.Next, ignorePrefix: true);

        //    if (!ignorePrefix && instruction.Previous?.OpCode.OpCodeType == OpCodeType.Prefix)
        //        return false;

        //    var flowControl = opCode.FlowControl;
        //    if (flowControl == FlowControl.Next || flowControl == FlowControl.Return)
        //        return false;

        //    if (instruction.Operand is Instruction target && target.Offset > instruction.Offset)
        //        return false;

        //    return true;
        //}

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
