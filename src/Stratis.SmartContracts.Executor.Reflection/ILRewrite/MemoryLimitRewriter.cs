using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{
    /// <summary>
    /// Must be applied to a module after the <see cref="GasInjectorRewriter"/>.
    /// </summary>
    public class MemoryLimitRewriter : IObserverMethodRewriter
    {
        public void Rewrite(MethodDefinition methodDefinition, ILProcessor il, ObserverRewriterContext context)
        {
            // Start from 2 - we added the 2 load variable instructions in ObserverRewriter
            for(int i=2; i< methodDefinition.Body.Instructions.Count; i++)
            {
                Instruction instruction = methodDefinition.Body.Instructions[i];

                if (instruction.OpCode.Code == Code.Newarr)
                {
                    il.InsertBefore(instruction, il.CreateLdlocBest(context.ObserverVariable));
                    il.InsertBefore(instruction, il.Create(OpCodes.Call, context.Observer.FlowThroughMemoryIntPtrMethod));
                    i += 2;
                }

                if (instruction.Operand is MethodReference called)
                {
                    if (called.DeclaringType.FullName == typeof(Array).FullName && called.Name == nameof(Array.Resize))
                    {
                        il.InsertBefore(instruction, il.CreateLdlocBest(context.ObserverVariable));
                        il.InsertBefore(instruction, il.Create(OpCodes.Call, context.Observer.FlowThroughMemoryInt32Method));
                        i += 2;
                    }

                    if (called.DeclaringType.FullName == typeof(string).FullName && called.Name == nameof(string.ToCharArray))
                    {
                        Instruction popJumpDest = il.Create(OpCodes.Pop);
                        il.InsertAfter(instruction,
                            il.Create(OpCodes.Dup),
                            il.Create(OpCodes.Ldlen),
                            il.CreateLdlocBest(context.ObserverVariable),
                            il.Create(OpCodes.Call, context.Observer.FlowThroughMemoryIntPtrMethod),
                            popJumpDest
                            );
                        i += 5;
                    }

                    if (called.DeclaringType.FullName == typeof(string).FullName && called.Name == nameof(string.Split))
                    {
                        Instruction popJumpDest = il.Create(OpCodes.Pop);
                        il.InsertAfter(instruction,
                            il.Create(OpCodes.Dup),
                            il.Create(OpCodes.Ldlen),
                            il.CreateLdlocBest(context.ObserverVariable),
                            il.Create(OpCodes.Call, context.Observer.FlowThroughMemoryIntPtrMethod),
                            popJumpDest
                            );
                        i += 5;
                    }

                    if (called.DeclaringType.FullName == typeof(string).FullName && called.Name == nameof(string.Concat))
                    {
                        Instruction popJumpDest = il.Create(OpCodes.Pop);
                        il.InsertAfter(instruction,
                            il.Create(OpCodes.Dup),
                            il.Create(OpCodes.Ldlen),
                            il.CreateLdlocBest(context.ObserverVariable),
                            il.Create(OpCodes.Call, context.Observer.FlowThroughMemoryIntPtrMethod),
                            popJumpDest
                            );
                        i += 5;
                    }

                    if (called.DeclaringType.FullName == typeof(string).FullName && called.Name == nameof(string.Join))
                    {
                        Instruction popJumpDest = il.Create(OpCodes.Pop);
                        il.InsertAfter(instruction,
                            il.Create(OpCodes.Dup),
                            il.Create(OpCodes.Ldlen),
                            il.CreateLdlocBest(context.ObserverVariable),
                            il.Create(OpCodes.Call, context.Observer.FlowThroughMemoryIntPtrMethod),
                            popJumpDest
                            );
                        i += 5;
                    }

                    if (called.DeclaringType.FullName == typeof(string).FullName && called.Name == ".ctor")
                    {
                        MethodDefinition method = ((MethodReference)instruction.Operand).Resolve();
                        // Ensure is the constructor with a count param (not all string constructors have a count param)
                        if (method.Parameters.Any(x => x.Name == "count"))
                        {
                            il.InsertBefore(instruction, il.CreateLdlocBest(context.ObserverVariable));
                            il.InsertBefore(instruction, il.Create(OpCodes.Call, context.Observer.FlowThroughMemoryInt32Method));
                            i += 2;
                        }
                    }

                }

            }

        }
    }
}
