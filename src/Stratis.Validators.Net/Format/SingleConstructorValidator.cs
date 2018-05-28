using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Stratis.Validators.Net.Format
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.TypeDefinition"/> contains only a single constructor
    /// </summary>
    public class SingleConstructorValidator : ITypeDefinitionValidator
    {
        public const string MissingConstructorError = "Contract must define a constructor";

        public const string SingleConstructorError = "Only a single constructor is allowed";

        public IEnumerable<ValidationResult> Validate(TypeDefinition typeDef)
        {
            List<MethodDefinition> constructors = typeDef.GetConstructors()?.ToList();

            if (constructors == null || !constructors.Any())
            {
                return new[]
                {
                    new ValidationResult(MissingConstructorError)
                };
            }

            if (constructors.Count > 1)
            {
                return new[]
                {
                    new ValidationResult(SingleConstructorError)
                };
            }

            return Enumerable.Empty<ValidationResult>();
        }
    }
}