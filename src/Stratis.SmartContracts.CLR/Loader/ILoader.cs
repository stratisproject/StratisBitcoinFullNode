using CSharpFunctionalExtensions;

namespace Stratis.SmartContracts.CLR.Loader
{
    public interface ILoader
    {
        Result<IContractAssembly> Load(ContractByteCode bytes);
    }
}
