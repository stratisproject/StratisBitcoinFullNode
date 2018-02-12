using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// Carries data that should be sent to a contract in a new transaction.
    /// Used by smart contract devs when calling a contract.
    /// </summary>
    public class TransactionDetails
    {
        public string ContractTypeName { get; set; }
        public string ContractMethodName { get; set; }
        public object[] Parameters { get; set; }
    }
}
