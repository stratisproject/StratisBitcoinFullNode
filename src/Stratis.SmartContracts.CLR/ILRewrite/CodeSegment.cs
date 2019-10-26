using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.CLR.ILRewrite
{
    /// <summary>
    /// A segment of code inside a method.
    /// </summary>
    public class CodeSegment
    {
        /// <summary>
        /// Method this segment is inside of.
        /// </summary>
        private readonly MethodDefinition methodDefinition;

        /// <summary>
        /// Instructions in this segment, always in the same order as they appear in the original method.
        /// </summary>
        public List<Instruction> Instructions { get; }

        public CodeSegment(MethodDefinition methodDefinition)
        {
            this.methodDefinition = methodDefinition;
            this.Instructions = new List<Instruction>();
        }

        /// <summary>
        /// Total gas cost to execute the instructions in this segment.
        /// </summary>
        public RuntimeObserver.Gas CalculateGasCost()
        {
            RuntimeObserver.Gas gasTally = (RuntimeObserver.Gas) 0;

            foreach (Instruction instruction in this.Instructions)
            {
                RuntimeObserver.Gas instructionCost = GasPriceList.InstructionOperationCost(instruction);
                gasTally = (RuntimeObserver.Gas)(gasTally + instructionCost);

                if (instruction.IsMethodCall())
                {
                    var methodToCall = (MethodReference)instruction.Operand;

                    // If it's a method outside this contract then we will add some cost.
                    if (this.methodDefinition.DeclaringType != methodToCall.DeclaringType)
                    {
                        RuntimeObserver.Gas methodCallCost = GasPriceList.MethodCallCost(methodToCall);
                        gasTally = (RuntimeObserver.Gas)(gasTally + methodCallCost);
                    }
                }
            }

            return gasTally;
        }
    }
}
