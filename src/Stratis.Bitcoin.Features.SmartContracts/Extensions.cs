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
            return transactions
                .Where(IsSmartContractExecTransaction);
        }

        /// <summary>
        /// Filters all transactions for those with contract create opcodes. 
        /// </summary>
        public static IEnumerable<Transaction> GetSmartContractCreateTransactions(this IEnumerable<Transaction> transactions)
        {
            return transactions
                .Where(IsSmartContractCreateTransaction);
        }

        private static bool IsSmartContractExecTransaction(Transaction tx)
        {
            return tx.Outputs.SingleOrDefault(s => s.ScriptPubKey.IsSmartContractExec) != null;
        }

        private static bool IsSmartContractCreateTransaction(Transaction tx)
        {
            return tx.Outputs.SingleOrDefault(s => s.ScriptPubKey.IsSmartContractCreate) != null;
        }
    }
}
