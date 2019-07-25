using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Stratis.SmartContracts.CLR.ILRewrite
{
    /// <summary>
    /// Rewrites the IL to spend 'memory units' inside the given <see cref="RuntimeObserver.Observer"/>.
    /// </summary>
    public class MemoryLimitRewriter : IObserverMethodRewriter
    {
        public void Rewrite(MethodDefinition methodDefinition, ILProcessor il, ObserverRewriterContext context)
        {
            // Start from 2 - we added the 2 load variable instructions in ObserverRewriter
            for(int i=2; i< methodDefinition.Body.Instructions.Count; i++)
            {
                // Keep current number of instructions to detect changes.
                int instructionCount = methodDefinition.Body.Instructions.Count;

                Instruction instruction = methodDefinition.Body.Instructions[i];

                if (instruction.OpCode.Code == Code.Newarr)
                {
                    this.CheckArrayCreationSize(instruction, il, context);
                }

                if (instruction.Operand is MethodReference called)
                {
                    this.PossiblyRewriteCalledMethod(called, instruction, il, context);
                }

                // If we rewrote, need to increase our iterator by the number of instructions we inserted.
                int instructionsAdded = methodDefinition.Body.Instructions.Count - instructionCount;
                i += instructionsAdded;
            }
        }

        /// <summary>
        /// Checks if it is one of a few method calls we need to check on.
        /// If so, does the necessary IL rewrite.
        /// </summary>
        private void PossiblyRewriteCalledMethod(MethodReference called, Instruction instruction, ILProcessor il, ObserverRewriterContext context)
        {
            if (called.DeclaringType.FullName == typeof(Array).FullName && called.Name == nameof(Array.Resize))
            {
                this.CheckArrayCreationSize(instruction, il, context);
                return;
            }

            if (called.DeclaringType.FullName == typeof(string).FullName)
            {
                if (called.Name == nameof(string.ToCharArray))
                {
                    this.CheckArrayReturnSize(instruction, il, context);
                }
                else if (called.Name == nameof(string.Split))
                {
                    this.CheckArrayReturnSize(instruction, il, context);
                }
                else if (called.Name == nameof(string.Concat))
                {
                    this.CheckArrayReturnSize(instruction, il, context);
                }
                else if (called.Name == nameof(string.Join))
                {
                    this.CheckArrayReturnSize(instruction, il, context);
                }
                else if (called.Name == ".ctor")
                {
                    this.CheckStringConstructor(instruction, il, context);
                }
            }
        }

        /// <summary>
        /// If is the specific string constructor that takes in a 'count' parameter, we need to check its size,
        /// </summary>
        private void CheckStringConstructor(Instruction instruction, ILProcessor il, ObserverRewriterContext context)
        {
            MethodDefinition method = ((MethodReference)instruction.Operand).Resolve();
            // Ensure is the constructor with a count param (not all string constructors have a count param)
            if (method.Parameters.Any(x => x.Name == "count"))
            {
                this.CheckArrayCreationSize(instruction, il, context);
            }
        }

        /// <summary>
        /// Insert instructions to check on the size of an array before it is created.
        /// Assumes the item on top of the stack at the moment is an int for the length of the array we're creating.
        /// </summary>
        private void CheckArrayCreationSize(Instruction instruction, ILProcessor il, ObserverRewriterContext context)
        {
            il.Body.SimplifyMacros();
            il.InsertBefore(instruction, il.CreateLdlocBest(context.ObserverVariable));
            il.InsertBefore(instruction, il.Create(OpCodes.Call, context.Observer.FlowThroughMemoryInt32Method));
            il.Body.OptimizeMacros();
        }

        /// <summary>
        /// Insert instructions to check on the size of an array that has just been pushed onto the top of the stack.
        /// </summary>
        private void CheckArrayReturnSize(Instruction instruction, ILProcessor il, ObserverRewriterContext context)
        {
            // TODO: We could do away with the pop on the end and not return anything from the observer method but we would need to cast to long correctly.
            il.Body.SimplifyMacros();
            il.InsertAfter(instruction,
                il.Create(OpCodes.Dup),
                il.Create(OpCodes.Ldlen),
                il.CreateLdlocBest(context.ObserverVariable),
                il.Create(OpCodes.Call, context.Observer.FlowThroughMemoryInt32Method),
                il.Create(OpCodes.Pop)
                );
            il.Body.OptimizeMacros();
        }
    }
}
