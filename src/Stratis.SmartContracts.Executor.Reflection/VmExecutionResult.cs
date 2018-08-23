using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.Executor.Reflection.ContractLogging;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class VmExecutionResult
    {
        public uint160 NewContractAddress { get; }

        public List<TransferInfo> InternalTransfers { get; }

        public Gas GasConsumed { get; }

        public object Result { get; }

        public Exception ExecutionException { get; }

        public IList<RawLog> RawLogs { get; }

        private VmExecutionResult(
            List<TransferInfo> internalTransfers, 
            Gas gasConsumed, 
            object result,
            IList<RawLog> logs = null,
            Exception e = null)
        {
            this.InternalTransfers = internalTransfers ?? new List<TransferInfo>();
            this.GasConsumed = gasConsumed;
            this.Result = result;
            this.RawLogs = logs ?? new List<RawLog>();
            this.ExecutionException = e;
        }

        private VmExecutionResult(
            uint160 newContractAddress,
            List<TransferInfo> internalTransfers,
            Gas gasConsumed,
            object result,
            IList<RawLog> logs = null,
            Exception e = null)
        {
            this.NewContractAddress = newContractAddress;
            this.InternalTransfers = internalTransfers ?? new List<TransferInfo>();
            this.GasConsumed = gasConsumed;
            this.Result = result;
            this.RawLogs = logs ?? new List<RawLog>();
            this.ExecutionException = e;
        }

        public static VmExecutionResult Success(List<TransferInfo> internalTransfers, Gas gasConsumed, object result, IList<RawLog> logs)
        {
            return new VmExecutionResult(internalTransfers, gasConsumed, result, logs);
        }

        public static VmExecutionResult CreationSuccess(uint160 newContractAddress, List<TransferInfo> internalTransfers, Gas gasConsumed, object result, IList<RawLog> logs)
        {
            return new VmExecutionResult(newContractAddress, internalTransfers, gasConsumed, result, logs);
        }

        public static VmExecutionResult Error(Gas gasConsumed, Exception e)
        {
            return new VmExecutionResult(null, gasConsumed, null, null, e);
        }
    }
}