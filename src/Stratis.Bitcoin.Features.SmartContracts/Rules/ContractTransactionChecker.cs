using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.Rules
{
    /// <summary>
    /// Shared logic for checking a contract transaction against a set of rules.
    /// </summary>
    public class ContractTransactionChecker
    {
        private readonly ICallDataSerializer callDataSerializer;

        public ContractTransactionChecker(ICallDataSerializer callDataSerializer)
        {
            this.callDataSerializer = callDataSerializer;
        }

        public Task RunAsync(RuleContext context, IEnumerable<IContractTransactionValidationRule> rules)
        {
            Block block = context.ValidationContext.BlockToValidate;

            List<IContractTransactionValidationRule> contractTransactionValidationRules = rules.ToList();

            foreach (Transaction transaction in block.Transactions)
            {
                this.CheckTransaction(transaction, contractTransactionValidationRules, null);
            }

            return Task.CompletedTask;
        }

        public void CheckTransaction(MempoolValidationContext context, IEnumerable<IContractTransactionValidationRule> rules)
        {
            this.CheckTransaction(context.Transaction, rules, context.Fees);
        }

        private void CheckTransaction(Transaction transaction, IEnumerable<IContractTransactionValidationRule> rules,
            Money suppliedBudget)
        {
            TxOut scTxOut = transaction.TryGetSmartContractTxOut();

            if (scTxOut == null)
            {
                // No SC output to validate.
                return;
            }

            Result<ContractTxData> callDataDeserializationResult = this.callDataSerializer.Deserialize(scTxOut.ScriptPubKey.ToBytes());

            if (callDataDeserializationResult.IsFailure)
            {
                new ConsensusError("invalid-calldata-format", string.Format("Invalid {0} format", typeof(ContractTxData).Name)).Throw();
            }

            ContractTxData txData = callDataDeserializationResult.Value;

            foreach (IContractTransactionValidationRule rule in rules)
            {
                rule.CheckContractTransaction(txData, suppliedBudget);
            }
        }
    }
}