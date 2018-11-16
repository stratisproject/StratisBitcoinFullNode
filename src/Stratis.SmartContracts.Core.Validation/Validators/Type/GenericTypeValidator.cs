using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Stratis.ModuleValidation.Net;
using Stratis.SmartContracts.Core.Validation.Validators.Method;

namespace Stratis.SmartContracts.Core.Validation.Validators.Type
{
    /// <summary>
    /// Ensures that a given type doesn't have any generic parameters.
    /// </summary>
    public class GenericTypeValidator : ITypeDefinitionValidator
    {
        public IEnumerable<ValidationResult> Validate(TypeDefinition type)
        {
            if (!type.HasGenericParameters)
                return Enumerable.Empty<MethodDefinitionValidationResult>();

            return new List<ValidationResult>
            {
                new GenericTypeValidationResult($"{type.FullName} is invalid [contains generic parameter]")
            };
        }

        public class GenericTypeValidationResult : TypeDefinitionValidationResult
        {
            public GenericTypeValidationResult(string message) : base(message)
            {
            }
        }
    }
}
