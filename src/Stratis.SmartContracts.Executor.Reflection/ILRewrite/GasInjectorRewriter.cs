using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{
    /// <summary>
    /// Rewrites a method to spend gas as execution occurs.
    /// </summary>
    public class GasInjectorRewriter : IObserverMethodRewriter
    {
        /// <summary>
        /// All branching opcodes.
        /// </summary>
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

        /// <summary>
        /// All opcodes that call to a method.
        /// </summary>
        private static readonly HashSet<OpCode> CallingOps = new HashSet<OpCode>
        {
            OpCodes.Call,
            OpCodes.Calli,
            OpCodes.Callvirt
        };

        /// <inheritdoc />
        public void Rewrite(MethodDefinition methodDefinition, ILProcessor il, ObserverRewriterContext context)
        {
            List<Instruction> branches = methodDefinition.Body.Instructions.Where(x => BranchingOps.Contains(x.OpCode)).ToList();
            List<Instruction> branchTos = branches.Select(x => (Instruction)x.Operand).ToList();

            Gas gasTally = Gas.None;

            Dictionary<Instruction, Gas> gasToSpendForSegment = new Dictionary<Instruction, Gas>();

            // To account  for variable load at start.
            int position = 2;
            Instruction currentSegmentStart = methodDefinition.Body.Instructions[position];

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

                AddSpendGasMethodBeforeInstruction(methodDefinition, context.Observer, context.ObserverVariable, injectAfterInstruction, gasToSpendForSegment[instruction]);
            }
        }

        /// <summary>
        /// Adds a call to SpendGas from the RuntimeObserver before the given instruction.
        /// </summary>
        private static void AddSpendGasMethodBeforeInstruction(MethodDefinition methodDefinition, ObserverReferences observer, VariableDefinition variable, Instruction instruction, Gas opcodeCount)
        {
            ILProcessor il = methodDefinition.Body.GetILProcessor();
            il.Body.SimplifyMacros();
            il.InsertBefore(instruction, il.CreateLdlocBest(variable)); // load observer
            il.InsertBefore(instruction, il.Create(OpCodes.Ldc_I8, (long)opcodeCount.Value)); // load gas amount
            il.InsertBefore(instruction, il.Create(OpCodes.Call, observer.SpendGasMethod)); // trigger method
            il.Body.OptimizeMacros();
        }
    }
}
