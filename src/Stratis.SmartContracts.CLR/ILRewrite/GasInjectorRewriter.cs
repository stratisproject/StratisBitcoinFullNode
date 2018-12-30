using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Stratis.SmartContracts.CLR.ILRewrite
{
    /// <summary>
    /// Rewrites a method to spend gas as execution occurs.
    /// </summary>
    public class GasInjectorRewriter : IObserverMethodRewriter
    {
        /// <inheritdoc />
        public void Rewrite(MethodDefinition methodDefinition, ILProcessor il, ObserverRewriterContext context)
        {
            List<Instruction> branches = GetBranchingOps(methodDefinition).ToList();
            List<Instruction> branchTos = branches.Select(x => (Instruction)x.Operand).ToList();

            // Start from 2 because we setup Observer in first 2 instructions
            int position = 2;

            List<CodeSegment> segments = new List<CodeSegment>();

            var codeSegment = new CodeSegment(methodDefinition);

            while (position < methodDefinition.Body.Instructions.Count)
            {
                Instruction instruction = methodDefinition.Body.Instructions[position];

                bool added = false;

                // Start of a new segment. End last segment and start new one with this as the first instruction
                if (branchTos.Contains(instruction))
                {
                    if (codeSegment.Instructions.Any())
                        segments.Add(codeSegment);
                    codeSegment = new CodeSegment(methodDefinition);
                    codeSegment.Instructions.Add(instruction);
                    added = true;
                }

                // End of a segment. Add this as the last instruction and move onwards with a new segment
                if (branches.Contains(instruction))
                {
                    codeSegment.Instructions.Add(instruction);
                    segments.Add(codeSegment);
                    codeSegment = new CodeSegment(methodDefinition);
                    added = true;
                }

                // Just an in-between instruction. Add to current segment.
                if (!added)
                {
                    codeSegment.Instructions.Add(instruction);
                }

                position++;
            }

            // Got to end of the method. Add the last one if necessary
            if (!segments.Contains(codeSegment) && codeSegment.Instructions.Any())
                segments.Add(codeSegment);

            foreach (CodeSegment segment in segments)
            {
                AddSpendGasMethodBeforeInstruction(il, context.Observer, context.ObserverVariable, segment);
            }

            // All of the branches now need to point to the place 3 instructions earlier!
            foreach (Instruction branch in branches)
            {
                Instruction currentlyPointingTo = (Instruction) branch.Operand;
                branch.Operand = currentlyPointingTo.Previous.Previous.Previous;
            }

        }

        /// <summary>
        /// Adds a call to SpendGas from the RuntimeObserver before the given instruction.
        /// </summary>
        private static void AddSpendGasMethodBeforeInstruction(ILProcessor il, ObserverReferences observer, VariableDefinition variable, CodeSegment codeSegment)
        {
            Instruction first = codeSegment.Instructions.First();
            Instruction newFirst = il.CreateLdlocBest(variable);
            long segmentCost = (long) codeSegment.CalculateGasCost().Value;

            il.Body.SimplifyMacros();
            il.InsertBefore(first, newFirst); // load observer
            il.InsertBefore(first, il.Create(OpCodes.Ldc_I8, (long) segmentCost)); // load gas amount
            il.InsertBefore(first, il.Create(OpCodes.Call, observer.SpendGasMethod)); // trigger method
            il.Body.OptimizeMacros();
        }

        private static IEnumerable<Instruction> GetBranchingOps(MethodDefinition methodDefinition)
        {
            return methodDefinition.Body.Instructions.Where(x => x.IsBranch());
        }
    }
}
