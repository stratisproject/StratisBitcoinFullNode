using System;
using Stratis.SmartContracts.Core;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class ContractInvocationResult : IContractInvocationResult
    {
        public bool IsSuccess { get; }
        public ContractInvocationErrorType InvocationErrorType { get; }
        public ContractErrorMessage ErrorMessage { get; }
        public object Return { get; }

        private ContractInvocationResult(object result)
        {
            this.IsSuccess = true;
            this.Return = result;
        }

        private ContractInvocationResult(ContractInvocationErrorType errorType, ContractErrorMessage errorMessage)
        {
            this.IsSuccess = false;
            this.InvocationErrorType = errorType;
            this.ErrorMessage = errorMessage;
        }

        public static ContractInvocationResult Success(object result)
        {
            return new ContractInvocationResult(result);
        }

        /// <summary>
        /// Return invocation failure for cases outside the execution of contract code.
        /// </summary>
        public static ContractInvocationResult Failure(ContractInvocationErrorType errorType)
        {
            switch (errorType)
            {
                case ContractInvocationErrorType.MethodDoesNotExist:
                    return new ContractInvocationResult(errorType,  new ContractErrorMessage("Method does not exist on contract."));
                case ContractInvocationErrorType.MethodIsConstructor:
                    return new ContractInvocationResult(errorType, new ContractErrorMessage("Attempted to invoke constructor on existing contract."));
                case ContractInvocationErrorType.MethodIsPrivate:
                    return new ContractInvocationResult(errorType, new ContractErrorMessage("Attempted to invoke private method."));
                case ContractInvocationErrorType.ParameterCountIncorrect:
                    return new ContractInvocationResult(errorType, new ContractErrorMessage("Incorrect number of parameters passed to method."));
                case ContractInvocationErrorType.ParameterTypesDontMatch:
                    return new ContractInvocationResult(errorType, new ContractErrorMessage("Parameters sent don't match expected method parameters."));
                default:
                    throw new NotSupportedException($"Should use either {nameof(Success)} or {nameof(ExecutionFailure)} for this ContractInvocationErrorType.");
            }
        }

        /// <summary>
        /// Return invocation failure for cases related to execution inside contract code.
        /// </summary>
        public static ContractInvocationResult ExecutionFailure(ContractInvocationErrorType errorType, Exception exception)
        {
            return new ContractInvocationResult(errorType, new ContractErrorMessage(exception.ToString()));
        }
    }
}