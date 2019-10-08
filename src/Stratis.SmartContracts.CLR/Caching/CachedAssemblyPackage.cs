using Stratis.SmartContracts.CLR.Loader;

namespace Stratis.SmartContracts.CLR.Caching
{
    public class CachedAssemblyPackage
    {
        public IContractAssembly Assembly { get; set; }

        public IContractModuleDefinition ModuleDefinition { get; set; }
    }
}
