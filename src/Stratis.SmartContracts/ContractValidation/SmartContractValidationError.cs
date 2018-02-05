using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.ContractValidation
{
    public class SmartContractValidationError
    {
        public string Message { get; set; }

        public SmartContractValidationError(string message)
        {
            Message = message;
        }

        public override string ToString()
        {
            return Message;
        }
    }
}
