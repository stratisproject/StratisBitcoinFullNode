using System;
using System.Collections.Generic;
using Mono.Cecil;
using Stratis.SmartContracts.CLR.Validation.Policy;
using Stratis.SmartContracts.CLR.Validation.Validators;

namespace Stratis.SmartContracts.CLR.Validation
{
    /// <summary>
    /// Defines a policy for validating a <see cref="ModuleDefinition"/> and its member hierarchy
    /// </summary>
    public class ValidationPolicy
    {
        private readonly List<IModuleDefinitionValidator> moduleDefValidators = new List<IModuleDefinitionValidator>();

        private readonly List<(ITypeDefinitionValidator, Func<TypeDefinition, bool>)> typeDefValidators = new List<(ITypeDefinitionValidator, Func<TypeDefinition, bool>)>();

        private readonly List<IFieldDefinitionValidator> fieldDefValidators = new List<IFieldDefinitionValidator>();

        private readonly List<IMethodDefinitionValidator> methodDefValidators = new List<IMethodDefinitionValidator>();

        private readonly List<IInstructionValidator> instructionValidators = new List<IInstructionValidator>();

        private readonly List<IMemberReferenceValidator> memberRefValidators = new List<IMemberReferenceValidator>();

        private readonly List<IParameterDefinitionValidator> parameterValidators = new List<IParameterDefinitionValidator>();

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

        public ValidationPolicy TypeDefValidator(ITypeDefinitionValidator validator)
        {
            this.typeDefValidators.Add((validator, t => true));
            return this;
        }

        public ValidationPolicy TypeDefValidator(ITypeDefinitionValidator validator, Func<TypeDefinition, bool> filter)
        {
            this.typeDefValidators.Add((validator, filter));
            return this;
        }


        public ValidationPolicy NestedTypeDefValidator(ITypeDefinitionValidator validator)
        {
            this.typeDefValidators.Add((validator, t => t.IsNested));
            return this;
        }

        public IEnumerable<(ITypeDefinitionValidator, Func<TypeDefinition, bool>)> TypeDefValidators =>
            this.typeDefValidators;

        public ValidationPolicy FieldDefValidator(IFieldDefinitionValidator validator)
        {
            this.fieldDefValidators.Add(validator);
            return this;
        }

        public IEnumerable<IFieldDefinitionValidator> FieldDefValidators => this.fieldDefValidators;

        public ValidationPolicy MethodDefValidator(IMethodDefinitionValidator validator)
        {
            this.methodDefValidators.Add(validator);
            return this;
        }

        public IEnumerable<IMethodDefinitionValidator> MethodDefValidators => this.methodDefValidators;

        public ValidationPolicy MethodParamValidator(IParameterDefinitionValidator validator)
        {
            this.parameterValidators.Add(validator);
            return this;
        }

        public IEnumerable<IParameterDefinitionValidator> ParameterValidators => this.parameterValidators;

        public ValidationPolicy WhitelistValidator(WhitelistPolicy policy)
        {
            var typeRefValidator = new WhitelistValidator(new WhitelistPolicyFilter(policy));
            this.instructionValidators.Add(typeRefValidator);
            return this;
        }
        
        public ValidationPolicy InstructionValidator(IInstructionValidator validator)
        {
            this.instructionValidators.Add(validator);
            return this;
        }
        
        public IEnumerable<IInstructionValidator> InstructionValidators => this.instructionValidators;

        public ValidationPolicy MemberReferenceValidator(IMemberReferenceValidator validator)
        {
            this.memberRefValidators.Add(validator);
            return this;
        }

        public IEnumerable<IMemberReferenceValidator> MemberRefValidators =>
            this.memberRefValidators;

        public ValidationPolicy ModuleDefValidator(IModuleDefinitionValidator validator)
        {
            this.moduleDefValidators.Add(validator);
            return this;
        }
    }
}