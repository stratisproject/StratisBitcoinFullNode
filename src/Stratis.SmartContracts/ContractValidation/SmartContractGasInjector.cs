using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.SmartContracts.ContractValidation
{
    public class SmartContractGasInjector
    {
        private const string GasMethod = "System.Void Stratis.SmartContracts.CompiledSmartContract::SpendGas(System.UInt32)";

        private static readonly HashSet<OpCode> _branchingOps = new HashSet<OpCode>
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

        private static readonly HashSet<OpCode> _callingOps = new HashSet<OpCode>
        {
            OpCodes.Call,
            OpCodes.Calli,
            OpCodes.Callvirt
        };

        public void AddGasCalculationToContract(TypeDefinition contractType, TypeDefinition baseType)
        {
            // Get gas expend method
            MethodDefinition gasMethod = baseType.Methods.First(m => m.FullName == GasMethod);
            MethodReference gasMethodReference = contractType.Module.Import(gasMethod);

            foreach (MethodDefinition method in contractType.Methods)
            {
                InjectSpendGasMethod(method, gasMethodReference);
            }
        }

        private void InjectSpendGasMethod(MethodDefinition methodDefinition, MethodReference gasMethod)
        {
            int position = 0;
            List<Instruction> branches = methodDefinition.Body.Instructions.Where(x => _branchingOps.Contains(x.OpCode)).ToList();
            List<Instruction> branchTos = branches.Select(x => (Instruction)x.Operand).ToList();

            Instruction currentSegmentStart = methodDefinition.Body.Instructions.FirstOrDefault();
            int currentSegmentCount = 0;

            Dictionary<Instruction, int> gasToSpendForSegment = new Dictionary<Instruction, int>();

            while (position < methodDefinition.Body.Instructions.Count)
            {
                var instruction = methodDefinition.Body.Instructions[position];

                // is the end of a segment. Include the current instruction in the count.
                if (branches.Contains(instruction))
                {
                    gasToSpendForSegment.Add(currentSegmentStart, currentSegmentCount + 1);
                    currentSegmentCount = 0;
                    position++;
                    if (position == methodDefinition.Body.Instructions.Count)
                        break;
                    currentSegmentStart = methodDefinition.Body.Instructions[position];
                }
                // is the start of a new segment. Don't include the current instruction in count.
                else if (branchTos.Contains(instruction) && instruction != currentSegmentStart)
                {
                    gasToSpendForSegment.Add(currentSegmentStart, currentSegmentCount);
                    currentSegmentCount = 0;
                    currentSegmentStart = instruction;
                    position++;
                }
                // is a call to another method
                else if (_callingOps.Contains(instruction.OpCode))
                {
                    var methodToCall = (MethodReference) instruction.Operand;
                    // If it's a method inside this contract then the gas will be injected no worries.
                    if (methodToCall.DeclaringType == methodDefinition.DeclaringType)
                    {
                        position++;
                        currentSegmentCount++;
                    }
                    // If it's a method outside this contract then we will need to get some average in future.
                    else
                    {
                        position++;
                        currentSegmentCount++;
                    }
                }
                // any other instruction. just increase counter.
                else
                {
                    position++;
                    currentSegmentCount++;
                }
            }

            if (!gasToSpendForSegment.ContainsKey(currentSegmentStart))
                gasToSpendForSegment.Add(currentSegmentStart, currentSegmentCount);

            foreach (var instruction in gasToSpendForSegment.Keys)
            {
                AddSpendGasMethodBeforeInstruction(methodDefinition, gasMethod, instruction, gasToSpendForSegment[instruction]);
            }

            var il = methodDefinition.Body.GetILProcessor();
            foreach (var instruction in branches)
            {
                var oldReference = (Instruction)instruction.Operand;
                var newReference = oldReference.Previous.Previous.Previous; // 3 were inserted
                var newInstruction = il.Create(instruction.OpCode, newReference);
                il.Replace(instruction, newInstruction);
            }
        }


        private void AddSpendGasMethodBeforeInstruction(MethodDefinition methodDefinition, MethodReference gasMethod, Instruction instruction, int opcodeCount)
        {
            var il = methodDefinition.Body.GetILProcessor();
            var ldarg0 = il.Create(OpCodes.Ldarg_0);
            var gasInstruction = il.Create(OpCodes.Call, gasMethod);
            var pushInstruction = il.Create(OpCodes.Ldc_I4, opcodeCount);
            il.InsertBefore(instruction, ldarg0);
            il.InsertBefore(instruction, pushInstruction);
            il.InsertBefore(instruction, gasInstruction);
        }
    }
}
