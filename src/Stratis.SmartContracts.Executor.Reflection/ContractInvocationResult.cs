using System;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class ContractInvocationResult : IContractInvocationResult
    {
        public bool IsSuccess { get; }
        public ContractInvocationErrorType InvocationErrorType { get; }
        public Exception Exception { get; }
        public object Return { get; }

        private ContractInvocationResult(object result)
        {
            this.IsSuccess = true;
            this.Return = result;
        }

        private ContractInvocationResult(ContractInvocationErrorType errorType)
        {
            this.IsSuccess = false;
            this.InvocationErrorType = errorType;
        }

        private ContractInvocationResult(ContractInvocationErrorType errorType, Exception exception)
        {
            this.IsSuccess = false;
            this.InvocationErrorType = errorType;
            this.Exception = exception;
        }

        public static ContractInvocationResult Success(object result)
        {
            return new ContractInvocationResult(result);
        }

        public static ContractInvocationResult Failure(ContractInvocationErrorType errorType)
        {
            return new ContractInvocationResult(errorType);
        }

        public static ContractInvocationResult Failure(ContractInvocationErrorType errorType, Exception exception)
        {
            return new ContractInvocationResult(errorType);
        }
    }
}