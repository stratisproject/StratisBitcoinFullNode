using System;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class VmExecutionResult
    {
        public object Result { get; }

        public Exception ExecutionException { get; }

        public string Type { get; }

        private VmExecutionResult(object result,
            string type = null,
            Exception e = null)
        {
            this.Result = result;
            this.Type = type;
            this.ExecutionException = e;
        }

        public static VmExecutionResult Success(object result, string type)
        {
            return new VmExecutionResult(result, type);
        }

        public static VmExecutionResult Error(Exception e)
        {
            return new VmExecutionResult(null, null, e);
        }
    }
}