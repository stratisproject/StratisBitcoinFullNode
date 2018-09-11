using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using RuntimeObserver;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{
    /// <summary>
    /// Rewrites a module to include an <see cref="Observer"/> that tracks gas usage. 
    /// </summary>
    public class ObserverRewriter : IILRewriter
    {
        public const string InjectedNamespace = "<Stratis>";
        public const string InjectedTypeName = "<RuntimeObserverInstance>";
        public const string InjectedPropertyName = "Instance";

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

        /// <summary>
        /// The Observer instance that will be used to track resource usage inside the rewritten modules.
        /// </summary>
        private readonly Observer observerToInject; 

        public ObserverRewriter(Observer observer)
        {
            this.observerToInject = observer;
        }

        public ModuleDefinition Rewrite(ModuleDefinition module)
        {
            Guid id = Guid.NewGuid();

            FieldDefinition observerInstanceField = GetObserverInstance(module, id);
            var observer = new ObserverReferences(observerInstanceField, module);

            foreach (TypeDefinition type in module.Types)
            {
                RewriteType(type, observer);
            }

            ObserverInstances.Set(id, this.observerToInject);

            return module;
        }

        /// <summary>
        /// Inserts a static type into the module which gives access to an instance of <see cref="Observer"/>.
        /// Because this is injected per module, it counts as a separate type and will not be a shared static.
        /// </summary>
        private FieldDefinition GetObserverInstance(ModuleDefinition module, Guid id)
        {
            // Add new type that can't be instantiated
            var instanceType = new TypeDefinition(
                InjectedNamespace, InjectedTypeName,
                TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.NotPublic,
                module.ImportReference(typeof(object))
            );

            // Add a field - an instance of our Observer!
            var instanceField = new FieldDefinition(
                InjectedPropertyName,
                FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly,
                module.ImportReference(typeof(Observer))
            );
            instanceType.Fields.Add(instanceField);

            // When this type is created, retrieve the Observer from our global static dictionary so it can be used.
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
                return; // don't inject on our injected type.

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
    }
}
