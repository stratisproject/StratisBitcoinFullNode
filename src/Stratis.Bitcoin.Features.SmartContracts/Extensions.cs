using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public static class Extensions
    {
        /// <summary>
        /// Filters all transactions for those with smart contract exec opcodes
        /// A transaction should only have one smart contract exec output
        /// </summary>
        public static IEnumerable<Transaction> GetSmartContractExecTransactions(this IEnumerable<Transaction> transactions)
        {
            return transactions.Where(x=> x.IsSmartContractExecTransaction());
        }

        /// <summary>
        /// Filters all transactions for those with contract create opcodes. 
        /// </summary>
        public static IEnumerable<Transaction> GetSmartContractCreateTransactions(this IEnumerable<Transaction> transactions)
        {
            return transactions.Where(x => x.IsSmartContractCreateTransaction());
        }

        public static bool IsSmartContractExecTransaction(this Transaction tx)
        {
            return tx.Outputs.Any(s => s.ScriptPubKey.IsSmartContractExec);
        }

        public static bool IsSmartContractCreateTransaction(this Transaction tx)
        {
            return tx.Outputs.Any(x => x.ScriptPubKey.IsSmartContractCreate);
        }

        public static bool IsSmartContractSpendTransaction(this Transaction tx)
        {
            return tx.Inputs.Any(s => s.ScriptSig.IsSmartContractSpend);
        }
    }
}
