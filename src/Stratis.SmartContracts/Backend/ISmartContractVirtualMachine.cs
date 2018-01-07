using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.Backend
{
    internal interface ISmartContractVirtualMachine
    {
        SmartContractExecutionResult ExecuteMethod(byte[] contractCode, SmartContractExecutionContext context);
        //SmartContractExecutionResult CreateContract(byte[] contractCode, SmartContractExecutionContext executionContext);
        //SmartContractExecutionResult LoadContractAndRun(byte[] contractCode, SmartContractExecutionContext executionContext);
    }
}
