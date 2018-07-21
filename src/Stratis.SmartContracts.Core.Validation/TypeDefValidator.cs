using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.ModuleValidation.Net;

namespace Stratis.SmartContracts.Core.Validation
{
    public class ModuleDefValidator
    {
        private readonly ValidationPolicy policy;

        private readonly TypeDefValidator typeDefValidator;

        public ModuleDefValidator(ValidationPolicy policy)
        {
            this.policy = policy;
            this.typeDefValidator = new TypeDefValidator(policy);
        }

        public IEnumerable<ValidationResult> Validate(ModuleDefinition module)
        {
            var results = new List<ValidationResult>();

            this.ValidateModule(results, module);

            var type = module.Types.FirstOrDefault(t =>
                t.FullName != "<Module>" && t.FullName != "<PrivateImplementationDetails>");

            if (type == null) return results;

            var result = this.typeDefValidator.Validate(type);
            results.AddRange(result);

            return results;
        }

        private void ValidateModule(List<ValidationResult> results, ModuleDefinition module)
        {
            foreach (var validator in this.policy.ModuleDefValidators)
            {
                results.AddRange(validator.Validate(module));
            }
        }

    }
    public class TypeDefValidator
    {
        private readonly ValidationPolicy policy;

        public TypeDefValidator(ValidationPolicy policy)
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

        private void ValidateMethods(List<ValidationResult> results, TypeDefinition type)
        {
            // Validate methods
            foreach (MethodDefinition method in type.Methods)
            {
                if (!method.HasBody)
                    return;

                if (method.Body.Instructions.Count == 0)
                    return;

                this.ValidateParameters(results, type, method);

                foreach (var validator in this.policy.MethodDefValidators)
                {
                    results.AddRange(validator.Validate(method));
                }

                this.ValidateInstructions(results, type, method);               
            }
        }

        private void ValidateParameters(List<ValidationResult> results, TypeDefinition type, MethodDefinition method)
        {
            if (this.policy.ParameterValidators.Any())
            {
                foreach (var parameter in method.Parameters)
                {
                    foreach (var (validate, result) in this.policy.ParameterValidators)
                    {
                        if (validate(method, parameter))
                            results.Add(result(type, method, parameter));
                    }
                }
            }
        }

        private void ValidateInstructions(List<ValidationResult> results, TypeDefinition type, MethodDefinition method)
        {
            if (!this.policy.InstructionValidators.Any() && !this.policy.MemberRefValidators.Any()) 
                return;

            // Validate instructions
            foreach (Instruction instruction in method.Body.Instructions)
            {
                foreach (var validator in this.policy.InstructionValidators)
                {
                    results.AddRange(validator.Validate(instruction, method));

                    this.ValidateMemberReferences(results, type, method, instruction);
                }
            }
        }

        private void ValidateMemberReferences(
            List<ValidationResult> results, 
            TypeDefinition type,
            MethodDefinition method,
            Instruction instruction)
        {
            if (!this.policy.MemberRefValidators.Any())
                return;

            // Validate member reference instructions
            if (!(instruction.Operand is MemberReference reference))
                return;

            foreach (var (validate, result) in this.policy.MemberRefValidators)
            {
                if (validate(reference))
                    results.Add(result(type, method, reference));
            }
        }

        private void ValidateNestedTypes(List<ValidationResult> results, TypeDefinition type)
        {
            // Recursively validate any nested Types
            foreach (TypeDefinition nestedType in type.NestedTypes)
            {
                results.AddRange(Validate(nestedType));
            }
        }

        private void ValidateFields(List<ValidationResult> results, TypeDefinition type)
        {
            // Validate the TypeDef's fields
            if (!this.policy.FieldDefValidators.Any()) 
                return;

            foreach (var field in type.Fields)
            {
                foreach (var (validate, result) in this.policy.FieldDefValidators)
                {
                    if (validate(field))
                        results.Add(result(type, field));
                }
            }
        }

        private void ValidateTypeDef(List<ValidationResult> results, TypeDefinition type)
        {
            // Validate the type def using TypeDefinition validators
            if (!this.policy.TypeDefValidators.Any()) 
                return;

            foreach (var (nestedPolicy, validator) in this.policy.TypeDefValidators)
            {
                if (type.IsNested && nestedPolicy == NestedTypePolicy.Ignore) continue;

                results.AddRange(validator.Validate(type));
            }
        }
    }
}