using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.ModuleValidation.Net;
using Stratis.ModuleValidation.Net.Determinism;
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
        private readonly List<IModuleDefinitionValidator> moduleDefValidators = new List<IModuleDefinitionValidator>();

        private readonly List<(NestedTypePolicy, ITypeDefinitionValidator)> typeDefValidators =
            new List<(NestedTypePolicy, ITypeDefinitionValidator)>();

        private readonly List<(Func<FieldDefinition, bool>, Func<TypeDefinition, FieldDefinition, ValidationResult>)> fieldDefValidators =
            new List<(Func<FieldDefinition, bool>, Func<TypeDefinition, FieldDefinition, ValidationResult>)>();

        private readonly List<IMethodDefinitionValidator> methodDefValidators = new List<IMethodDefinitionValidator>();

        private readonly List<IInstructionValidator> instructionValidators = new List<IInstructionValidator>();

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
        

        public IEnumerable<IModuleDefinitionValidator> ModuleDefValidators => this.moduleDefValidators;

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

        public ValidationPolicy MethodDefValidator(IMethodDefinitionValidator validator)
        {
            this.methodDefValidators.Add(validator);
            return this;
        }

        public IEnumerable<IMethodDefinitionValidator> MethodDefValidators => this.methodDefValidators;

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
            this.instructionValidators.Add(typeRefValidator);
            return this;
        }
        
        public ValidationPolicy InstructionValidator(FloatValidator validator)
        {
            this.instructionValidators.Add(validator);
            return this;
        }
        
        public IEnumerable<IInstructionValidator> InstructionValidators => this.instructionValidators;

        public ValidationPolicy MemberReferenceValidator(Func<MemberReference, bool> validator,
            Func<TypeDefinition, MethodDefinition, MemberReference, ValidationResult> errorMessageFactory)
        {
            this.memberRefValidators.Add((validator, errorMessageFactory));
            return this;
        }

        public IEnumerable<(Func<MemberReference, bool>, Func<TypeDefinition, MethodDefinition, MemberReference, ValidationResult>)> MemberRefValidators =>
            this.memberRefValidators;

        public ValidationPolicy ModuleDefValidator(IModuleDefinitionValidator validator)
        {
            this.moduleDefValidators.Add(validator);
            return this;
        }
    }
}