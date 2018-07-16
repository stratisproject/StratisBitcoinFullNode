using System;

namespace Stratis.SmartContracts.Executor.Reflection.Lifecycle
{
    /// <summary>
    /// The result of a Smart Contract lifecycle operation
    /// </summary>
    public class LifecycleResult
    {
        public LifecycleResult(SmartContract obj)
        {
            this.Success = true;
            this.Object = obj;
        }

        public LifecycleResult(Exception e)
        {
            this.Success = false;
            this.Exception = e;
        }

        public Exception Exception { get; }

        public bool Success { get; }

        public SmartContract Object { get; }
    }
}