using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.TypeDefinition"/> contains only a single constructor
    /// </summary>
    public class SingleConstructorValidator : ITypeDefinitionValidator
    {
        public const string MissingConstructorError = "Contract must define a constructor";

        public const string SingleConstructorError = "Only a single constructor is allowed";

        public IEnumerable<FormatValidationError> Validate(TypeDefinition typeDef)
        {
            List<MethodDefinition> constructors = typeDef.GetConstructors()?.ToList();

            if (constructors == null || !constructors.Any())
            {
                return new[]
                {
                    new FormatValidationError(MissingConstructorError)
                };
            }

            if (constructors.Count > 1)
            {
                return new[]
                {
                    new FormatValidationError(SingleConstructorError)
                };
            }

            return Enumerable.Empty<FormatValidationError>();
        }
    }
}