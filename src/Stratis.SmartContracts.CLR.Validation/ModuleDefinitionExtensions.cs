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
        /// Get the 'real' types from the module. i.e. not added by the compiler or framework.
        /// </summary>
        public static IEnumerable<TypeDefinition> GetContractTypes(this ModuleDefinition moduleDefinition)
        {            
            return moduleDefinition.Types
                .Where(x => x.BaseType != null)
                .Where(x => x.IsClass && !x.IsAbstract)
                .Where(x => x.BaseType.FullName == InheritsSmartContractValidator.SmartContractType);
        }
    }
}
