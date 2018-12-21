using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation
{
    /// <summary>
    /// Util holds types which we need to ignore when retrieving types from module.
    /// These types are added by the compiler / framework and not the developer.
    /// </summary>
    public static class ModuleDefinitionExtensions
    {
        /// <summary>
        /// Determines whether a type is a contract type.
        /// </summary>
        public static bool IsContractType(this TypeDefinition typeDefinition)
        {
            return typeDefinition.IsClass &&
                   !typeDefinition.IsAbstract &&
                   typeDefinition.BaseType != null &&
                   typeDefinition.BaseType.FullName == InheritsSmartContractValidator.SmartContractType;
        }

        /// <summary>
        /// Get the contract types from the module.
        /// </summary>
        public static IEnumerable<TypeDefinition> GetContractTypes(this ModuleDefinition moduleDefinition)
        {
            return moduleDefinition.Types.Where(IsContractType);
        }
    }
}
