using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.ContractValidation.Result
{
    internal class ContractValidationError
    {
        public string Message { get; set; }

        public ContractValidationError(string message)
        {
            Message = message;
        }
    }
}
