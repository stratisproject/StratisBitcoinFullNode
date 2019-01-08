using System;
using Stratis.SmartContracts.Core;

namespace Stratis.SmartContracts.CLR
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
            string errorMessage = GetErrorMessage(errorType);
            return new ContractInvocationResult(errorType, new ContractErrorMessage(errorMessage));
        }

        /// <summary>
        /// Return invocation failure for cases related to execution inside contract code.
        /// </summary>
        public static ContractInvocationResult ExecutionFailure(ContractInvocationErrorType errorType, Exception exception)
        {
            return new ContractInvocationResult(errorType, new ContractErrorMessage(exception.ToString()));
        }

        private static string GetErrorMessage(ContractInvocationErrorType errorType)
        {
            switch (errorType)
            {
                case ContractInvocationErrorType.MethodDoesNotExist:
                    return ContractInvocationErrors.MethodDoesNotExist;
                case ContractInvocationErrorType.MethodIsConstructor:
                    return ContractInvocationErrors.MethodIsConstructor;
                case ContractInvocationErrorType.MethodIsPrivate:
                    return ContractInvocationErrors.MethodIsPrivate;
                case ContractInvocationErrorType.ParameterCountIncorrect:
                    return ContractInvocationErrors.ParameterCountIncorrect;
                case ContractInvocationErrorType.ParameterTypesDontMatch:
                    return ContractInvocationErrors.ParameterTypesDontMatch;
                default:
                    throw new NotSupportedException($"Should use either {nameof(Success)} or {nameof(ExecutionFailure)} for this ContractInvocationErrorType.");
            }
        }
    }
}