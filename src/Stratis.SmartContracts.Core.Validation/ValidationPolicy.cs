using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.ModuleValidation.Net;
using Stratis.ModuleValidation.Net.Format;
using Stratis.SmartContracts.Core.Validation.Policy;
using Stratis.SmartContracts.Core.Validation.Validators;
using Stratis.SmartContracts.Core.Validation.Validators.Module;

namespace Stratis.SmartContracts.Core.Validation
{
    public enum NestedTypePolicy
    {
        Validate,
        Ignore
    }

    public class ValidationPolicy
    {
        private readonly List<(Func<ModuleDefinition, bool>, Func<ModuleDefinition, ValidationResult>)> moduleDefValidators =
            new List<(Func<ModuleDefinition, bool>, Func<ModuleDefinition, ValidationResult>)>();

        private readonly List<(NestedTypePolicy, ITypeDefinitionValidator)> typeDefValidators =
            new List<(NestedTypePolicy, ITypeDefinitionValidator)>();

        private readonly List<(Func<FieldDefinition, bool>, Func<TypeDefinition, FieldDefinition, ValidationResult>)> fieldDefValidators =
            new List<(Func<FieldDefinition, bool>, Func<TypeDefinition, FieldDefinition, ValidationResult>)>();

        private readonly List<(Func<MethodDefinition, bool>, Func<TypeDefinition, MethodDefinition, ValidationResult>)> methodDefValidators =
            new List<(Func<MethodDefinition, bool>, Func<TypeDefinition, MethodDefinition, ValidationResult>)>();

        private readonly List<(Func<Instruction, bool>, Func<TypeDefinition, MethodDefinition, Instruction, ValidationResult>)> instructionValidators =
            new List<(Func<Instruction, bool>, Func<TypeDefinition, MethodDefinition, Instruction, ValidationResult>)>();

        private readonly List<(Func<MemberReference, bool>, Func<TypeDefinition, MethodDefinition, MemberReference, ValidationResult>)> memberRefValidators =
            new List<(Func<MemberReference, bool>, Func<TypeDefinition, MethodDefinition, MemberReference, ValidationResult>)>();

        private readonly List<(Func<MethodDefinition, ParameterDefinition, bool>, Func<TypeDefinition, MethodDefinition, ParameterDefinition, ValidationResult>)> parameterValidators =
            new List<(Func<MethodDefinition, ParameterDefinition, bool>, Func<TypeDefinition, MethodDefinition, ParameterDefinition, ValidationResult>)>();

        public static ValidationPolicy FromExisting(IEnumerable<ValidationPolicy> existing)
        {
            var policy = new ValidationPolicy();

            foreach (ValidationPolicy p in existing)
            {
                policy.fieldDefValidators.AddRange(p.fieldDefValidators);
                policy.moduleDefValidators.AddRange(p.moduleDefValidators);
                policy.instructionValidators.AddRange(p.instructionValidators);
                policy.memberRefValidators.AddRange(p.memberRefValidators);
                policy.methodDefValidators.AddRange(p.methodDefValidators);
                policy.parameterValidators.AddRange(p.parameterValidators);
                policy.typeDefValidators.AddRange(p.typeDefValidators);
            }

            return policy;
        }

        public ValidationPolicy ModuleDefValidator(Func<ModuleDefinition, bool> validator,
            Func<ModuleDefinition, ValidationResult> errorMessageFactory)
        {
            this.moduleDefValidators.Add((validator, errorMessageFactory));
            return this;
        }

        public IEnumerable<(Func<ModuleDefinition, bool>, Func<ModuleDefinition, ValidationResult>)> ModuleDefValidators =>
            this.moduleDefValidators;

        public ValidationPolicy TypeDefValidator(ITypeDefinitionValidator validator, NestedTypePolicy nestedTypePolicy = NestedTypePolicy.Validate)
        {
            this.typeDefValidators.Add((nestedTypePolicy, validator));
            return this;
        }

        public ValidationPolicy NestedTypeDefValidator(ITypeDefinitionValidator validator)
        {
            this.typeDefValidators.Add((NestedTypePolicy.Validate, new NestedValidator(validator)));
            return this;
        }

        public IEnumerable<(NestedTypePolicy, ITypeDefinitionValidator)> TypeDefValidators =>
            this.typeDefValidators;

        public ValidationPolicy FieldDefValidator(Func<FieldDefinition, bool> validator,
            Func<TypeDefinition, FieldDefinition, ValidationResult> errorMessageFactory)
        {
            this.fieldDefValidators.Add((validator, errorMessageFactory));
            return this;
        }

        public IEnumerable<(Func<FieldDefinition, bool>, Func<TypeDefinition, FieldDefinition, ValidationResult>)> FieldDefValidators =>
            this.fieldDefValidators;

        public ValidationPolicy MethodDefValidator(Func<MethodDefinition, bool> validator,
            Func<TypeDefinition, MethodDefinition, ValidationResult> errorMessageFactory)
        {
            this.methodDefValidators.Add((validator, errorMessageFactory));
            return this;
        }

        public IEnumerable<(Func<MethodDefinition, bool>, Func<TypeDefinition, MethodDefinition, ValidationResult>)> MethodDefValidators =>
            this.methodDefValidators;

        public ValidationPolicy MethodParamValidator(Func<MethodDefinition, ParameterDefinition, bool> validator,
            Func<TypeDefinition, MethodDefinition, ParameterDefinition, ValidationResult> errorMessageFactory)
        {
            this.parameterValidators.Add((validator, errorMessageFactory));
            return this;
        }

        public IEnumerable<(Func<MethodDefinition, ParameterDefinition, bool>, Func<TypeDefinition, MethodDefinition, ParameterDefinition, ValidationResult>)> ParameterValidators =>
            this.parameterValidators;

        public ValidationPolicy WhitelistValidator(WhitelistPolicy policy)
        {
            var typeRefValidator = new TypeReferenceValidator(new WhitelistPolicyFilter(policy));

            this.instructionValidators.Add((
                i => typeRefValidator.Validate(i).Any(),
                (t, m, i) =>
                {
                    var memberRef = (i.Operand as MemberReference);
                    return new TypeReferenceValidator.DeniedMemberValidationResult(
                        m.FullName, "Whitelist", $"{memberRef.FullName} is not allowed");
                }));

            return this;
        }

        public ValidationPolicy InstructionValidator(Func<Instruction, bool> validator,
            Func<TypeDefinition, MethodDefinition, Instruction, ValidationResult> errorMessageFactory)
        {
            this.instructionValidators.Add((validator, errorMessageFactory));
            return this;
        }

        public IEnumerable<(Func<Instruction, bool>, Func<TypeDefinition, MethodDefinition, Instruction, ValidationResult>)> InstructionValidators =>
            this.instructionValidators;

        public ValidationPolicy MemberReferenceValidator(Func<MemberReference, bool> validator,
            Func<TypeDefinition, MethodDefinition, MemberReference, ValidationResult> errorMessageFactory)
        {
            this.memberRefValidators.Add((validator, errorMessageFactory));
            return this;
        }

        public IEnumerable<(Func<MemberReference, bool>, Func<TypeDefinition, MethodDefinition, MemberReference, ValidationResult>)> MemberRefValidators =>
            this.memberRefValidators;

        public ValidationPolicy ModuleDefValidator(AssemblyReferenceValidator validator)
        {
            this.moduleDefValidators.Add((v => validator.Validate(v).Any(), m => new TypeDefinitionValidationResult(m.Name)));
            return this;
        }
    }
}