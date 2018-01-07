using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.ContractValidation.Result
{
    public class SmartContractValidationError
    {
        public string Message { get; set; }

        public SmartContractValidationError(string message)
        {
            Message = message;
        }
    }
}
