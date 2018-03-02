using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public static class Extensions
    {
        /// <summary>
        /// Filters all transactions for those with smart contract exec opcodes
        /// A transaction should only have one smart contract exec output
        /// </summary>
        /// <param name="transactions"></param>
        /// <returns></returns>
        public static IEnumerable<Transaction> SmartContractTransactions(this IEnumerable<Transaction> transactions)
        {
            return transactions
                .Where(IsSmartContractTransaction);
        }

        private static bool IsSmartContractTransaction(Transaction tx)
        {
            return tx.Outputs.SingleOrDefault(s => s.ScriptPubKey.IsSmartContractExec) != null;
        }
    }
}
