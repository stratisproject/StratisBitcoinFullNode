using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class VmExecutionResult
    {
        public uint160 NewContractAddress { get; }

        public List<TransferInfo> InternalTransfers { get; }

        public Gas GasConsumed { get; }

        public object Result { get; }

        public Exception ExecutionException { get; }

        public IList<Log> Logs { get; }

        private VmExecutionResult(
            List<TransferInfo> internalTransfers, 
            Gas gasConsumed, 
            object result,
            IList<Log> logs = null,
            Exception e = null)
        {
            this.InternalTransfers = internalTransfers ?? new List<TransferInfo>();
            this.GasConsumed = gasConsumed;
            this.Result = result;
            this.Logs = logs;
            this.ExecutionException = e;
        }

        private VmExecutionResult(
            uint160 newContractAddress,
            List<TransferInfo> internalTransfers,
            Gas gasConsumed,
            object result,
            IList<Log> logs = null,
            Exception e = null)
        {
            this.NewContractAddress = newContractAddress;
            this.InternalTransfers = internalTransfers ?? new List<TransferInfo>();
            this.GasConsumed = gasConsumed;
            this.Result = result;
            this.Logs = logs;
            this.ExecutionException = e;
        }

        public static VmExecutionResult Success(List<TransferInfo> internalTransfers, Gas gasConsumed, object result, IList<Log> logs)
        {
            return new VmExecutionResult(internalTransfers, gasConsumed, result, logs);
        }

        public static VmExecutionResult CreationSuccess(uint160 newContractAddress, List<TransferInfo> internalTransfers, Gas gasConsumed, object result, IList<Log> logs)
        {
            return new VmExecutionResult(newContractAddress, internalTransfers, gasConsumed, result, logs);
        }

        public static VmExecutionResult Error(Gas gasConsumed, Exception e)
        {
            return new VmExecutionResult(null, gasConsumed, null, null, e);
        }
    }
}