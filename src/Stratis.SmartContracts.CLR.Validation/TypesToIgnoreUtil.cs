using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation
{
    /// <summary>
    /// Util holds types which we need to ignore when retrieving types from module.
    /// These types are added by the compiler / framework and not the developer.
    /// </summary>
    public static class TypesToIgnoreUtil
    {
        public static readonly HashSet<string> Ignore = new HashSet<string>
        {
            "<Module>", // Part of every module
            "<PrivateImplementationDetails>" // Added when constructing an array
        };

        /// <summary>
        /// Get the 'real' types from the module. i.e. not added by the compiler or framework.
        /// </summary>
        public static IEnumerable<TypeDefinition> GetDevelopedTypes(this ModuleDefinition moduleDefinition)
        {
            return moduleDefinition.Types.Where(x => !Ignore.Contains(x.FullName));
        }
    }
}
