using CSharpFunctionalExtensions;

namespace Stratis.SmartContracts.Executor.Reflection.Loader
{
    public interface ILoader
    {
        Result<IContractAssembly> Load(ContractByteCode bytes);
    }
}
