using System;
using Stratis.SmartContracts.Core.Exceptions;

namespace Stratis.SmartContracts.CLR.Exceptions
{
    /// <summary>
    /// Thrown when the amount of memory consumed by contract execution 
    /// exceeds the network-enforced limit.
    /// </summary>
    public class MemoryConsumptionException : SmartContractException
    {
        public MemoryConsumptionException(string message) : base(message) { }
    }
}
