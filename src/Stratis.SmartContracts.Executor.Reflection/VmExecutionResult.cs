using System;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class VmExecutionResult
    {
        public InternalTransferList InternalTransferList { get; }

        public Gas GasConsumed { get; }

        public object Result { get; }

        public Exception ExecutionException { get; }

        private VmExecutionResult(
            InternalTransferList internalTransfers, 
            Gas gasConsumed, 
            object result,
            Exception e = null)
        {
            this.InternalTransferList = internalTransfers;
            this.GasConsumed = gasConsumed;
            this.Result = result;
            this.ExecutionException = e;
        }

        public static VmExecutionResult Success(InternalTransferList internalTransfers, Gas gasConsumed, object result)
        {
            return new VmExecutionResult(internalTransfers, gasConsumed, result);
        }

        public static VmExecutionResult Error(Gas gasConsumed, Exception e)
        {
            return new VmExecutionResult(null, gasConsumed, null, e);
        }
    }
}