namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface IContractInvocationResult
    {
        bool IsSuccess { get; }

        ContractInvocationErrorType InvocationErrorType { get; }

        object Return { get; }
    }
}