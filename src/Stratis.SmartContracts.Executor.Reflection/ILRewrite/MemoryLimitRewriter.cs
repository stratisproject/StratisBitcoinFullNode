using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using RuntimeObserver;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{
    /// <summary>
    /// Must be applied to a module after the <see cref="ObserverRewriter"/>.
    /// </summary>
    public class MemoryLimitRewriter : IILRewriter
    {
        public ModuleDefinition Rewrite(ModuleDefinition module)
        {
            FieldDefinition instance = GetObserverInstance(module);
            ObserverReferences observer = new ObserverReferences(instance, module);
            foreach (TypeDefinition type in module.Types)
            {
                RewriteType(type, observer);
            }
            return module;
        }

        /// <summary>
        /// Get the same Observer instance that was injected. 
        /// TODO: Pass around the Observer instance for performance rather than retrieving a second time.
        /// </summary>
        private FieldDefinition GetObserverInstance(ModuleDefinition module)
        {
            TypeDefinition injectedType = module.GetType(ObserverRewriter.InjectedNamespace, ObserverRewriter.InjectedTypeName);

            if (injectedType == null)
                throw new NotSupportedException("Can only rewrite assemblies with an Observer injected.");

            return injectedType.Fields.FirstOrDefault(x => x.Name == ObserverRewriter.InjectedPropertyName);
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
                return; // don't inject on our injected type.

            if (!methodDefinition.HasBody || methodDefinition.Body.Instructions.Count == 0)
                return; // don't inject on method without a Body 

            ILProcessor il = methodDefinition.Body.GetILProcessor();
            VariableDefinition observerVariable = methodDefinition.Body.Variables.First(x => x.VariableType.Name == typeof(Observer).Name);

            // Start from 2 - we added 2 instructions in ObserverRewriter
            for(int i=2; i< methodDefinition.Body.Instructions.Count; i++)
            {
                Instruction instruction = methodDefinition.Body.Instructions[i];

                if (instruction.OpCode.Code == Code.Newarr)
                {
                    il.InsertBefore(instruction, il.CreateLdlocBest(observerVariable));
                    il.InsertBefore(instruction, il.Create(OpCodes.Call, observer.FlowThroughMemoryIntPtrMethod));
                    i += 2;
                }

                if (instruction.Operand is MethodReference called)
                {
                    if (called.DeclaringType.FullName == typeof(Array).FullName && called.Name == nameof(Array.Resize))
                    {
                        il.InsertBefore(instruction, il.CreateLdlocBest(observerVariable));
                        il.InsertBefore(instruction, il.Create(OpCodes.Call, observer.FlowThroughMemoryInt32Method));
                        i += 2;
                    }

                    // TODO: Investigate what this is doing - straight from Unbreakable.
                    if (called.DeclaringType.FullName == typeof(string).FullName && called.Name == nameof(string.ToCharArray))
                    {
                        Instruction popJumpDest = il.Create(OpCodes.Pop);
                        il.InsertAfter(instruction,
                            il.Create(OpCodes.Dup),
                            il.Create(OpCodes.Dup),
                            il.Create(OpCodes.Brfalse, popJumpDest),
                            il.Create(OpCodes.Ldlen),
                            il.CreateLdlocBest(observerVariable),
                            il.Create(OpCodes.Call, observer.FlowThroughMemoryIntPtrMethod),
                            popJumpDest
                            );
                        i += 7;
                    }

                    if (called.DeclaringType.FullName == typeof(string).FullName && called.Name == ".ctor")
                    {
                        MethodDefinition method = ((MethodReference)instruction.Operand).Resolve();
                        // Ensure is the constructor with a count param (not all string constructors have a count param)
                        if (method.Parameters.Any(x => x.Name == "count"))
                        {
                            il.InsertBefore(instruction, il.CreateLdlocBest(observerVariable));
                            il.InsertBefore(instruction, il.Create(OpCodes.Call, observer.FlowThroughMemoryInt32Method));
                            i += 2;
                        }
                    }


                }
                

            }

        }
    }
}
