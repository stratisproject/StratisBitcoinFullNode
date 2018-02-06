using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.ContractValidation
{
    public class SmartContractValidationError
    {
        public string MethodName { get; set; }

        public string MethodFullName { get; set; }

        public string Message { get; set; }

        public string ErrorType { get; set; }

        public SmartContractValidationError(string message)
        {
            Message = message;
        }

        public SmartContractValidationError(string methodName, string methodFullName, string errorType, string message)
            : this(message)
        {
            MethodName = methodName;
            MethodFullName = methodFullName;
            ErrorType = errorType;
        }

        public override string ToString()
        {
            return Message;
        }
    }
}
