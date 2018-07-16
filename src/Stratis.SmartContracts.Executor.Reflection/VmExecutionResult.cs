using System;
using System.Collections.Generic;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class VmExecutionResult
    {
        public List<TransferInfo> InternalTransfers { get; }

        public Gas GasConsumed { get; }

        public object Result { get; }

        public Exception ExecutionException { get; }

        private VmExecutionResult(
            List<TransferInfo> internalTransfers, 
            Gas gasConsumed, 
            object result,
            Exception e = null)
        {
            this.InternalTransfers = internalTransfers ?? new List<TransferInfo>();
            this.GasConsumed = gasConsumed;
            this.Result = result;
            this.ExecutionException = e;
        }

        public static VmExecutionResult Success(List<TransferInfo> internalTransfers, Gas gasConsumed, object result)
        {
            return new VmExecutionResult(internalTransfers, gasConsumed, result);
        }

        public static VmExecutionResult Error(Gas gasConsumed, Exception e)
        {
            return new VmExecutionResult(null, gasConsumed, null, e);
        }
    }
}