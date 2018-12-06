using System;

namespace Stratis.SmartContracts.RuntimeObserver
{
    /// <summary>
    /// Thrown when the amount of memory consumed by contract execution 
    /// exceeds the network-enforced limit.
    /// </summary>
    public class MemoryConsumptionException : Exception
    {
        public MemoryConsumptionException(string message) : base(message) { }
    }
}
