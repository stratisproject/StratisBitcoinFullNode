using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.ModuleValidation.Net;

namespace Stratis.SmartContracts.Core.Validation
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.MethodDefinition"/> does not create new objects
    /// </summary>
    public class NewObjectValidator : IMethodDefinitionValidator
    {
        public static readonly string ErrorType = "Use Of New";
        
        private IEnumerable<string> typeWhitelist;

        public NewObjectValidator(IEnumerable<string> typeWhitelist)
        {
            this.typeWhitelist = typeWhitelist;
        }

        public IEnumerable<ValidationResult> Validate(MethodDefinition method)
        {
            if (method.Body?.Instructions == null)
                return Enumerable.Empty<MethodDefinitionValidationResult>();

            var errors = new List<ValidationResult>();

            var newObjs = method.Body.Instructions.Where(i => i.OpCode == OpCodes.Initobj || i.OpCode == OpCodes.Newobj);

            foreach (var newObj in newObjs)
            {
                if (newObj.Operand is TypeReference typeReference)
                {
                    if (!this.typeWhitelist.Contains(typeReference.FullName))
                    {
                        errors.Add(new NewObjectValidationResult(
                            method.FullName,
                            ErrorType,
                            $"{method.FullName} is invalid [{ErrorType} {typeReference.FullName}]"
                        ));
                    }
                }

                if (newObj.Operand is MethodReference methodReference)
                {
                    if (methodReference.DeclaringType == null) 
                        continue;

                    if (!this.typeWhitelist.Contains(methodReference.DeclaringType.FullName))
                    {
                        errors.Add(new NewObjectValidationResult(
                            method.FullName,
                            ErrorType,
                            $"{method.FullName} is invalid [{ErrorType} {methodReference.DeclaringType.FullName}]"
                        ));
                    }
                }
            }

            return errors;
        }

        public class NewObjectValidationResult : MethodDefinitionValidationResult
        {
            public NewObjectValidationResult(string subjectName, string validationType, string message) : base(subjectName, validationType, message)
            {
            }
        }
    }
}