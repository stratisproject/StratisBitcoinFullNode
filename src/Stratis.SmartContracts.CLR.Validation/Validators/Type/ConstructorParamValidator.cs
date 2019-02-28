using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Stratis.SmartContracts.CLR.Validation.Validators.Type
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.TypeDefinition"/>'s constructor has a first param of type <see cref="ISmartContractState"/>
    /// </summary>
    public class ConstructorParamValidator : ITypeDefinitionValidator
    {
        public const string InvalidParamError = "The first constructor argument must be an ISmartContractState object";

        public bool Validate(TypeDefinition typeDef)
        {
            MethodDefinition constructor = typeDef
                .GetConstructors()?
                .FirstOrDefault();

            if (constructor == null)
            {
                // Not up to us to validate this here
                return false;
            }

            ParameterDefinition firstArg = constructor.Parameters.FirstOrDefault();

            return firstArg == null || !IsSmartContractState(firstArg);
        }

        public TypeDefinitionValidationResult CreateError(TypeDefinition type)
        {
            return new TypeDefinitionValidationResult(InvalidParamError);
        }

        private static bool IsSmartContractState(ParameterDefinition firstArg)
        {
            return firstArg.ParameterType.FullName == typeof(ISmartContractState).FullName;
        }

        IEnumerable<ValidationResult> ITypeDefinitionValidator.Validate(TypeDefinition typeDef)
        {
            MethodDefinition constructor = typeDef
                .GetConstructors()?
                .FirstOrDefault();

            if (constructor == null)
            {
                // Not up to us to validate this here
                return Enumerable.Empty<ValidationResult>();
            }

            ParameterDefinition firstArg = constructor.Parameters.FirstOrDefault();

            bool valid = firstArg != null && IsSmartContractState(firstArg);

            if (!valid)
            {
                return new [] { this.CreateError(typeDef) };
            }

            return Enumerable.Empty<ValidationResult>();
        }
    }
}