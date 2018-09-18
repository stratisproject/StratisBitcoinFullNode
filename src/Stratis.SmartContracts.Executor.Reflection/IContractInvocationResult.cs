using Stratis.SmartContracts.Core;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface IContractInvocationResult
    {
        bool IsSuccess { get; }

        ContractInvocationErrorType InvocationErrorType { get; }

        ContractErrorMessage ErrorMessage { get; }

        object Return { get; }
    }
}