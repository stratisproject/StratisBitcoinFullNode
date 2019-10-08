using Stratis.SmartContracts.CLR.Loader;

namespace Stratis.SmartContracts.CLR.Caching
{
    /// <summary>
    /// Holds the items required to execute a contract.
    /// </summary>
    public class CachedAssemblyPackage
    {
        public IContractAssembly Assembly { get; }

        public IContractModuleDefinition ModuleDefinition { get; }


        public CachedAssemblyPackage(IContractAssembly assembly, IContractModuleDefinition moduleDefinition)
        {
            this.Assembly = assembly;
            this.ModuleDefinition = moduleDefinition;
        }
    }
}
