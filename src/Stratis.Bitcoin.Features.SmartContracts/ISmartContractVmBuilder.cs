using Stratis.Bitcoin.Builder;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public interface ISmartContractVmBuilder
    {
        IFullNodeBuilder UseReflectionExecutor();
        IFullNodeBuilder UseAnotherExecutor();
    }
}
