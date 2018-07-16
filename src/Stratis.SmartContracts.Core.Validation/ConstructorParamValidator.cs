using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Stratis.ModuleValidation.Net;

namespace Stratis.SmartContracts.Core.Validation
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.TypeDefinition"/>'s constructor has a first param of type <see cref="ISmartContractState"/>
    /// </summary>
    public class ConstructorParamValidator : ITypeDefinitionValidator
    {
        public const string InvalidParamError = "The first constructor argument must be an ISmartContractState object";

        public IEnumerable<ValidationResult> Validate(TypeDefinition typeDef)
        {
            MethodDefinition constructor = typeDef
                .GetConstructors()?
                .FirstOrDefault();

            if (constructor == null)
            {
                // Not up to us to validate this here
                return Enumerable.Empty<TypeDefinitionValidationResult>();
            }

            ParameterDefinition firstArg = constructor.Parameters.FirstOrDefault();

            if (firstArg == null || !IsSmartContractState(firstArg))
            {
                return new []
                {
                    new TypeDefinitionValidationResult(InvalidParamError)
                };
            }

            return Enumerable.Empty<TypeDefinitionValidationResult>();
        }

        private static bool IsSmartContractState(ParameterDefinition firstArg)
        {
            return firstArg.ParameterType.FullName == typeof(ISmartContractState).FullName;
        }
    }
}