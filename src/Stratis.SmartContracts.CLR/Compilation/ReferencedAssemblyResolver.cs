using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stratis.SmartContracts.Standards;

namespace Stratis.SmartContracts.CLR.Compilation
{
    /// <summary>
    /// Resolves the assemblies allowed to be used in a Smart Contract
    /// </summary>
    public static class ReferencedAssemblyResolver
    {
        // System.Runtime forwards to mscorlib, so we can only get its Assembly by name
        // ref. https://github.com/dotnet/corefx/issues/11601
        private static readonly Assembly Runtime = Assembly.Load("System.Runtime");
        private static readonly Assembly Core = typeof(object).Assembly;

        /// <summary>
        /// The set of Assemblies that a <see cref="SmartContract"/> is required to reference
        /// </summary>
        public static HashSet<Assembly> AllowedAssemblies = new HashSet<Assembly> {
                Runtime, 
                Core, 
                typeof(SmartContract).Assembly, 
                typeof(Enumerable).Assembly,
                typeof(IStandardToken).Assembly
            };
    }
}