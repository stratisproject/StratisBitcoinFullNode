using Stratis.SmartContracts.Core;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class VmExecutionResult
    {
        public object Result { get; }

        public ContractErrorMessage ErrorMessage { get; }

        public string Type { get; }

        private VmExecutionResult(object result,
            string type = null,
            ContractErrorMessage error = null)
        {
            this.Result = result;
            this.Type = type;
            this.ErrorMessage = error;
        }

        public static VmExecutionResult Success(object result, string type)
        {
            return new VmExecutionResult(result, type);
        }

        public static VmExecutionResult Error(ContractErrorMessage error)
        {
            return new VmExecutionResult(null, null, error);
        }
    }
}