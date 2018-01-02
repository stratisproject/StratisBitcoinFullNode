using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts
{
    public class TransactionDetails
    {
        public string ContractTypeName { get; set; }
        public string ContractMethodName { get; set; }
        public object[] Parameters { get; set; }
    }
}
