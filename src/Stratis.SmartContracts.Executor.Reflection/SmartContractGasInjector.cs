﻿using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public static class SmartContractGasInjector
    {
        private const string GasMethod = "System.Void Stratis.SmartContracts.SmartContract::SpendGas(System.UInt64)";

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

        public static TypeDefinition GetContractBaseType(TypeDefinition typeDefinition)
        {
            return typeDefinition.BaseType.Resolve();
        }
        
        public static ModuleDefinition AddGasCalculationToConstructor(ModuleDefinition moduleDefinition, string typeName)
        {
            return AddGasCalculationToContractMethodInternal(moduleDefinition, typeName, ".ctor");
        }

        /// <summary>
        /// Rewrites the IL of the given method on the <see cref="ModuleDefinition"/> by injects calls to SpendGas. If no method with that name exists, returns the original <see cref="ModuleDefinition"/>.
        /// </summary>
        public static ModuleDefinition AddGasCalculationToContractMethod(ModuleDefinition moduleDefinition, string typeName, string methodName)
        {
            return AddGasCalculationToContractMethodInternal(moduleDefinition, typeName, methodName);
        }

        private static ModuleDefinition AddGasCalculationToContractMethodInternal(ModuleDefinition moduleDefinition, string typeName, string methodName)
        {
            TypeDefinition contractType = moduleDefinition.Types.FirstOrDefault(x => x.Name == typeName);
            List<MethodDefinition> methods = contractType.Methods.Where(m => m.Name == methodName).ToList();

            // It's possible that a method references an overload of itself, which means that the overload could potentially be injected twice.
            // Because of this we need to keep track of which methods have been injected.
            HashSet<string> injectedMethods = new HashSet<string>();

            if (!methods.Any())
                return moduleDefinition;

            TypeDefinition baseType = GetContractBaseType(contractType);

            // Get gas spend method
            MethodDefinition gasMethod = baseType.Methods.First(m => m.FullName == GasMethod);
            MethodReference gasMethodReference = contractType.Module.ImportReference(gasMethod);

            foreach (var method in methods)
            {
                IEnumerable<MethodDefinition> referencedMethods = method.Body.Instructions
                    .Select(i => i.Operand)
                    .OfType<MethodDefinition>()
                    .ToList();

                InjectSpendGasMethod(method, gasMethodReference);

                injectedMethods.Add(method.FullName);

                foreach (MethodDefinition referencedMethod in referencedMethods)
                {
                    if (injectedMethods.Contains(referencedMethod.FullName))
                        continue;

                    InjectSpendGasMethod(referencedMethod, gasMethodReference);

                    injectedMethods.Add(referencedMethod.FullName);
                }
            }

            return moduleDefinition;
        }

        private static void InjectSpendGasMethod(MethodDefinition methodDefinition, MethodReference gasMethod)
        {
            int position = 0;
            List<Instruction> branches = methodDefinition.Body.Instructions.Where(x => BranchingOps.Contains(x.OpCode)).ToList();
            List<Instruction> branchTos = branches.Select(x => (Instruction)x.Operand).ToList();

            Instruction currentSegmentStart = methodDefinition.Body.Instructions.FirstOrDefault();
            Gas gasTally = Gas.None;

            Dictionary<Instruction, Gas> gasToSpendForSegment = new Dictionary<Instruction, Gas>();

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
                var injectAfterInstruction = instruction;

                // If it's a constructor we need to skip the first 3 instructions. 
                // These will always be invoking the base constructor
                // ldarg.0
                // ldarg.0
                // call SmartContract::ctor
                if (methodDefinition.IsConstructor)
                {
                    injectAfterInstruction = instruction.Next.Next.Next;
                }

                AddSpendGasMethodBeforeInstruction(methodDefinition, gasMethod, injectAfterInstruction, gasToSpendForSegment[instruction]);
            }

            ILProcessor il = methodDefinition.Body.GetILProcessor();
            foreach (Instruction instruction in branches)
            {
                var oldReference = (Instruction)instruction.Operand;
                Instruction newReference = oldReference.Previous.Previous.Previous; // 3 were inserted
                Instruction newInstruction = il.Create(instruction.OpCode, newReference);
                il.Replace(instruction, newInstruction);
            }
        }

        private static void AddSpendGasMethodBeforeInstruction(MethodDefinition methodDefinition, MethodReference gasMethod, Instruction instruction, Gas opcodeCount)
        {
            ILProcessor il = methodDefinition.Body.GetILProcessor();
            Instruction ldarg0 = il.Create(OpCodes.Ldarg_0);
            Instruction gasInstruction = il.Create(OpCodes.Call, gasMethod);
            Instruction pushInstruction = il.Create(OpCodes.Ldc_I8, (long)opcodeCount.Value);

            // Ref: https://stackoverflow.com/questions/16346155/cil-opcode-ldarg-0-is-used-even-though-there-are-no-arguments
            il.InsertBefore(instruction, ldarg0);

            // Add the instruction for pushing the opcode count onto the stack
            il.InsertBefore(instruction, pushInstruction);

            // Add the gas method call instruction
            il.InsertBefore(instruction, gasInstruction);
        }
    }
}