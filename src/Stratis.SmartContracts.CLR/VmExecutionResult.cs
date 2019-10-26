using Stratis.SmartContracts.Core;

namespace Stratis.SmartContracts.CLR
{
    public enum VmExecutionErrorKind
    {
        OutOfGas,
        OutOfResources,
        ValidationFailed,
        LoadFailed,
        InvocationFailed,
        RewriteFailed
    }
    
    public class VmExecutionResult
    {
        public VmExecutionError Error { get; }

        public VmExecutionSuccess Success { get; }

        public bool IsSuccess { get; }

        private VmExecutionResult(VmExecutionError error)
        {
            this.IsSuccess = false;
            this.Error = error;
        }

        private VmExecutionResult(VmExecutionSuccess success)
        {
            this.IsSuccess = true;
            this.Success = success;
        }

        public static VmExecutionResult Ok(object result, string type)
        {
            return new VmExecutionResult(new VmExecutionSuccess(result, type));
        }

        public static VmExecutionResult Fail(VmExecutionErrorKind errorKind, string error)
        {
            return new VmExecutionResult(new VmExecutionError(errorKind, new ContractErrorMessage(error)));
        }
    }

    public class VmExecutionSuccess
    {
        public VmExecutionSuccess(object result, string type)
        {
            this.Result = result;
            this.Type = type;
        }

        public object Result { get; }

        public string Type { get; }
    }

    public class VmExecutionError
    {
        public VmExecutionError(VmExecutionErrorKind errorKind, ContractErrorMessage errorMessage)
        {
            this.ErrorKind = errorKind;
            this.Message = errorMessage;
        }

        public VmExecutionErrorKind ErrorKind { get; }
        public ContractErrorMessage Message { get; }
    }
}