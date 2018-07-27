using System;
using System.Collections.Generic;
using NBitcoin;
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

        private VmExecutionResult(
            uint160 newContractAddress,
            List<TransferInfo> internalTransfers,
            Gas gasConsumed,
            object result,
            Exception e = null)
        {
            this.NewContractAddress = newContractAddress;
            this.InternalTransfers = internalTransfers ?? new List<TransferInfo>();
            this.GasConsumed = gasConsumed;
            this.Result = result;
            this.ExecutionException = e;
        }

        public static VmExecutionResult Success(List<TransferInfo> internalTransfers, Gas gasConsumed, object result)
        {
            return new VmExecutionResult(internalTransfers, gasConsumed, result);
        }

        public static VmExecutionResult CreationSuccess(uint160 newContractAddress, List<TransferInfo> internalTransfers, Gas gasConsumed, object result)
        {
            return new VmExecutionResult(newContractAddress, internalTransfers, gasConsumed, result);
        }

        public static VmExecutionResult Error(Gas gasConsumed, Exception e)
        {
            return new VmExecutionResult(null, gasConsumed, null, e);
        }
    }
}