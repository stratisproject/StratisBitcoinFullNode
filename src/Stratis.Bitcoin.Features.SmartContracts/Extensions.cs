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

        /// <summary>
        /// Whether the transaction has any outputs with ScriptPubKeys that are smart contract executions.
        /// </summary>
        public static bool IsSmartContractExecTransaction(this Transaction tx)
        {
            return tx.Outputs.Any(s => s.ScriptPubKey.IsSmartContractExec);
        }

        /// <summary>
        /// Whether the transaction has any outputs with ScriptPubKeys that are smart contract creations.
        /// </summary>
        public static bool IsSmartContractCreateTransaction(this Transaction tx)
        {
            return tx.Outputs.Any(x => x.ScriptPubKey.IsSmartContractCreate);
        }

        /// <summary>
        /// Whether the transaction has any inputs with ScriptSigs that are OP_SPENDS.
        /// </summary>
        public static bool IsSmartContractSpendTransaction(this Transaction tx)
        {
            return tx.Inputs.Any(s => s.ScriptSig.IsSmartContractSpend);
        }
    }
}
