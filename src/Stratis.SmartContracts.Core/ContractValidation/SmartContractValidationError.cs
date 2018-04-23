using Mono.Cecil;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public class SmartContractValidationError
    {
        public const string NonDeterministicMethodReference = "Non-deterministic method reference.";

        public string MethodName { get; set; }

        public string MethodFullName { get; set; }

        public string Message { get; set; }

        public string ErrorType { get; set; }

        public SmartContractValidationError(string message)
        {
            this.Message = message;
        }

        public SmartContractValidationError(MethodDefinition methodDefinition, string errorType, string message)
            : this(message)
        {
            this.MethodFullName = methodDefinition.FullName;
            this.MethodName = methodDefinition.Name;
            this.ErrorType = errorType;
        }

        public SmartContractValidationError(string methodName, string methodFullName, string errorType, string message)
            : this(message)
        {
            this.MethodName = methodName;
            this.MethodFullName = methodFullName;
            this.ErrorType = errorType;
        }

        /// <summary>
        /// Returns an error when a method is non-deterministic.
        /// </summary>
        public static SmartContractValidationError NonDeterministic(MethodDefinition userMethod)
        {
            return new SmartContractValidationError(userMethod, NonDeterministicMethodReference, $"Use of {userMethod.FullName} is not deterministic.");
        }

        /// <summary>
        /// Returns an error when a referenced method is non-deterministic in a containing method.
        /// <para>I.e. if in method A, method B is referenced and it is non-deterministic, use this method.</para>
        /// </summary>
        /// <param name="userMethod">The containing method.</param>
        /// <param name="referencedMethod">The method that is non-deterministic in the containing method.</param>
        public static SmartContractValidationError NonDeterministic(MethodDefinition userMethod, MethodDefinition referencedMethod)
        {
            return new SmartContractValidationError(userMethod, NonDeterministicMethodReference, $"Use of {referencedMethod.FullName} is not deterministic.");
        }

        public override string ToString()
        {
            return this.Message;
        }
    }
}