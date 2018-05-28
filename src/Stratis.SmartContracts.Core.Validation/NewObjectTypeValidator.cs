using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.ModuleValidation.Net;

namespace Stratis.SmartContracts.Core.Validation
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.MethodDefinition"/> only does not create objects whose Type is blacklisted
    /// </summary>
    public class NewObjectTypeValidator : IMethodDefinitionValidator
    {
        public static readonly string ErrorType = "Invalid New Object Type";

        public static readonly IEnumerable<string> BlacklistedTypes = new[]
        {
            typeof(System.Runtime.CompilerServices.TaskAwaiter).FullName
        };

        public IEnumerable<ValidationResult> Validate(MethodDefinition method)
        {
            if (method.Body?.Instructions == null)
                return Enumerable.Empty<MethodDefinitionValidationResult>();

            var errors = new List<ValidationResult>();

            IEnumerable<TypeReference> typeReferences = method.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Initobj)
                .Where(i => i.Operand is TypeReference)
                .Select(i => i.Operand as TypeReference);

            foreach (TypeReference typeReference in typeReferences)
            {
                if (BlacklistedTypes.Any(t => typeReference.FullName.Contains(t)))
                {
                    errors.Add(new MethodDefinitionValidationResult(
                        method,
                        ErrorType,
                        $"{method.FullName} is invalid [{ErrorType} {typeReference.FullName}]"
                    ));
                }
            }

            return errors;
        }
    }
}