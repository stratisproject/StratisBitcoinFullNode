using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates that a type contains only a single constructor
    /// </summary>
    public class SingleConstructorValidator
    {
        public const string MissingConstructorError = "Contract must define a constructor";

        public const string SingleConstructorError = "Only a single constructor is allowed";

        public IEnumerable<SmartContractValidationError> Validate(TypeDefinition typeDef)
        {
            List<MethodDefinition> constructors = typeDef.GetConstructors()?.ToList();

            if (constructors == null || !constructors.Any())
            {
                return new[]
                {
                    new SmartContractValidationError(MissingConstructorError)
                };
            }

            if (constructors.Count > 1)
            {
                return new []
                {
                    new SmartContractValidationError(SingleConstructorError)
                };
            }
            
            return Enumerable.Empty<SmartContractValidationError>();
        }        
    }
}