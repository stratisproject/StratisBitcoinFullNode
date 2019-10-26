using Stratis.SmartContracts.Core;

namespace Stratis.SmartContracts.CLR
{
    public interface IContractInvocationResult
    {
        bool IsSuccess { get; }

        ContractInvocationErrorType InvocationErrorType { get; }

        ContractErrorMessage ErrorMessage { get; }

        object Return { get; }
    }
}