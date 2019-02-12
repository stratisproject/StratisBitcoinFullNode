using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.CLR.Validation
{
    /// <summary>
    /// Validator for a <see cref="TypeDefinition"/> using a given <see cref="ValidationPolicy"/>
    /// </summary>
    public class TypePolicyValidator : ITypeDefinitionValidator
    {
        private readonly ValidationPolicy policy;

        public TypePolicyValidator(ValidationPolicy policy)
        {
            this.policy = policy;
        }

        public IEnumerable<ValidationResult> Validate(TypeDefinition type)
        {
            var results = new List<ValidationResult>();

            this.ValidateTypeDef(results, type);
            this.ValidateNestedTypes(results, type);
            this.ValidateFields(results, type);
            this.ValidateMethods(results, type);

            return results;
        }

        /// <summary>
        /// Validates the methods within a <see cref="TypeDefinition"/>
        /// </summary>
        private void ValidateMethods(List<ValidationResult> results, TypeDefinition type)
        {
            foreach (MethodDefinition method in type.Methods)
            {
                this.ValidateParameters(results, type, method);

                foreach (var validator in this.policy.MethodDefValidators)
                {
                    results.AddRange(validator.Validate(method));
                }

                this.ValidateInstructions(results, type, method);               
            }
        }

        /// <summary>
        /// Validates the parameters within a <see cref="MethodDefinition"/>
        /// </summary>
        private void ValidateParameters(List<ValidationResult> results, TypeDefinition type, MethodDefinition method)
        {
            if (!this.policy.ParameterValidators.Any())
                return;

            foreach (var parameter in method.Parameters)
            {
                foreach (var validator in this.policy.ParameterValidators)
                {
                    results.AddRange(validator.Validate(parameter));
                }
            }
        }

        /// <summary>
        /// Validates instructions in a <see cref="MethodDefinition"/>
        /// </summary>
        private void ValidateInstructions(List<ValidationResult> results, TypeDefinition type, MethodDefinition method)
        {
            if (!this.policy.InstructionValidators.Any() && !this.policy.MemberRefValidators.Any()) 
                return;

            if (method.Body == null)
            {
                return;
            }

            foreach (Instruction instruction in method.Body.Instructions)
            {
                foreach (var validator in this.policy.InstructionValidators)
                {
                    results.AddRange(validator.Validate(instruction, method));

                    this.ValidateMemberReferences(results, type, method, instruction);
                }
            }
        }

        /// <summary>
        /// Validate Types referred to by instructions that are member references
        /// </summary>        
        private void ValidateMemberReferences(
            List<ValidationResult> results, 
            TypeDefinition type,
            MethodDefinition method,
            Instruction instruction)
        {
            if (!this.policy.MemberRefValidators.Any())
                return;

            if (!(instruction.Operand is MemberReference reference))
                return;

            foreach (var validator in this.policy.MemberRefValidators)
            {
                results.AddRange(validator.Validate(reference));
            }
        }

        /// <summary>
        /// Recursively validates nested Types
        /// </summary>
        private void ValidateNestedTypes(List<ValidationResult> results, TypeDefinition type)
        {
            foreach (TypeDefinition nestedType in type.NestedTypes)
            {
                results.AddRange(Validate(nestedType));
            }
        }

        /// <summary>
        /// Validates a type definition's fields
        /// </summary>
        private void ValidateFields(List<ValidationResult> results, TypeDefinition type)
        {
            if (!this.policy.FieldDefValidators.Any()) 
                return;

            foreach (var field in type.Fields)
            {
                foreach (var validator in this.policy.FieldDefValidators)
                {
                    results.AddRange(validator.Validate(field));
                }
            }
        }

        /// <summary>
        /// Validate the type def using TypeDefinition validators
        /// </summary>
        private void ValidateTypeDef(List<ValidationResult> results, TypeDefinition type)
        {
            if (!this.policy.TypeDefValidators.Any()) 
                return;

            foreach (var (validator, shouldValidateTypeFilter) in this.policy.TypeDefValidators)
            {
                if (shouldValidateTypeFilter(type))
                {
                    results.AddRange(validator.Validate(type));
                }
            }
        }
    }
}