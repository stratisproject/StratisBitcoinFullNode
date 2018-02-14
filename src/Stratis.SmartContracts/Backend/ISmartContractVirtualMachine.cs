using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.Backend
{
    internal interface ISmartContractVirtualMachine
    {
        SmartContractExecutionResult ExecuteMethod(byte[] contractCode, string contractTypeName, string methodName,
            SmartContractExecutionContext context);
    }
}
