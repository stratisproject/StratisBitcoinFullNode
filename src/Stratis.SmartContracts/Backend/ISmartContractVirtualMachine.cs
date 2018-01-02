using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.Backend
{
    internal interface ISmartContractVirtualMachine
    {
        ExecutionResult CreateContract(byte[] contractCode, ExecutionContext executionContext);
        ExecutionResult LoadContractAndRun(byte[] contractCode, ExecutionContext executionContext);
    }
}
