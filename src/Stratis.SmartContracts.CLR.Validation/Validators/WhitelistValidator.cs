using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation.Validators
{
    /// <summary>
    /// Validates an instruction for usages of allowed Types
    /// </summary>
    public class WhitelistValidator : IInstructionValidator
    {
        private readonly WhitelistPolicyFilter whitelistPolicyFilter;

        public WhitelistValidator(WhitelistPolicyFilter whitelistPolicyFilter)
        {
            this.whitelistPolicyFilter = whitelistPolicyFilter;
        }

        public IEnumerable<ValidationResult> Validate(Mono.Cecil.Cil.Instruction instruction, MethodDefinition method)
        {
            if (!(instruction.Operand is MemberReference reference))
                return Enumerable.Empty<ValidationResult>();

            if (reference is MethodReference methodReference)
            {
                var validationResults = new List<ValidationResult>();
                validationResults.AddRange(this.ValidateReference(method, methodReference.DeclaringType, methodReference.Name));
                validationResults.AddRange(this.ValidateReference(method, methodReference.ReturnType));

                return validationResults;
            }

            if (reference is FieldReference fieldReference)
            {
                var validationResults = new List<ValidationResult>();
                validationResults.AddRange(this.ValidateReference(method, fieldReference.DeclaringType, fieldReference.Name));
                validationResults.AddRange(this.ValidateReference(method, fieldReference.FieldType));

                return validationResults;
            }

            if (reference is TypeReference typeReference)
            {
                return this.ValidateReference(method, typeReference);
            }

            return Enumerable.Empty<ValidationResult>();
        }

        private IEnumerable<ValidationResult> ValidateReference(MethodDefinition parent, TypeReference type, string memberName = null)
        {
            var results = new List<ValidationResult>();

            if (type.IsGenericParameter)
                return results;

            if (type.IsByReference)
            {
                results.AddRange(this.ValidateReference(parent, type.GetElementType()));

                // No further validation required
                return results;
            }

            if (type is GenericInstanceType generic)
            {
                results.AddRange(this.ValidateReference(parent, generic.ElementType));

                foreach (TypeReference argument in generic.GenericArguments)
                {
                    results.AddRange(this.ValidateReference(parent, argument));
                }

                return results;
            }

            if (type.IsArray)
            {
                results.AddRange(this.ValidateReference(parent, type.GetElementType()));

                return results;
            }

            // By the time we get here, we should have boiled it down to a base Type
            results.AddRange(this.ValidateWhitelist(parent, type, memberName));

            return results;
        }

        private IEnumerable<ValidationResult> ValidateWhitelist(MethodDefinition parent, TypeReference type,
            string memberName = null)
        {
            // Allows types from user module, they are Type definitions not Type references
            if (type is TypeDefinition)
            {
                yield break;
            }

            PolicyValidationResult result = this.whitelistPolicyFilter.Filter(type.Namespace, type.Name, memberName);

            switch (result.Kind)
            {
                case PolicyValidatorResultKind.DeniedNamespace:
                    string ns = string.IsNullOrWhiteSpace(type.Namespace) ? "\"\"" : type.Namespace;
                    yield return new DeniedNamespaceValidationResult(
                        parent.FullName,
                        "Whitelist", 
                        $"Type {type.Name} in Namespace {ns} is not allowed");
                    break;
                case PolicyValidatorResultKind.DeniedType:
                    yield return new DeniedTypeValidationResult(
                        parent.FullName,
                        "Whitelist",
                        $"Type {type.FullName} is not allowed");
                    break;
                case PolicyValidatorResultKind.DeniedMember:
                    yield return new DeniedMemberValidationResult(
                        parent.FullName,
                        "Whitelist",
                        $"Member {type.FullName}.{memberName} is not allowed");
                    break;
            }
        }

        public abstract class WhitelistValidationResult : ValidationResult 
        {
            protected WhitelistValidationResult(string subjectName, string validationType, string message) 
                : base(subjectName, validationType, message)
            {
            }
        }

        public class DeniedNamespaceValidationResult : WhitelistValidationResult
        {
            public DeniedNamespaceValidationResult(string subjectName, string validationType, string message)
                : base(subjectName, validationType, message)
            {
            }
        }

        public class DeniedTypeValidationResult : WhitelistValidationResult
        {
            public DeniedTypeValidationResult(string subjectName, string validationType, string message)
                : base(subjectName, validationType, message)
            {
            }
        }

        public class DeniedMemberValidationResult : WhitelistValidationResult
        {
            public DeniedMemberValidationResult(string subjectName, string validationType, string message)
                : base(subjectName, validationType, message)
            {
            }
        }
    }
}