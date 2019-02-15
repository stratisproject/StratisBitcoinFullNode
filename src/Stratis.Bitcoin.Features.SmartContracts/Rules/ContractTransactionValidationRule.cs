using System.Collections.Generic;
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
    /// Checks that smart contract transactions are in a valid format and the data is serialized correctly.
    /// </summary>
    public class ContractTransactionValidationRule : PartialValidationConsensusRule, ISmartContractMempoolRule
    {
        private readonly ICallDataSerializer callDataSerializer;

        private readonly IList<IContractTransactionValidationLogic> internalRules;

        public ContractTransactionValidationRule(ICallDataSerializer callDataSerializer, IList<IContractTransactionValidationLogic> internalRules)
        {
            this.callDataSerializer = callDataSerializer;
            this.internalRules = internalRules;
        }

        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;

            foreach (Transaction transaction in block.Transactions)
            {
                this.CheckTransaction(transaction, null);
            }

            return Task.CompletedTask;
        }

        public void CheckTransaction(MempoolValidationContext context)
        {
            this.CheckTransaction(context.Transaction, context.Fees);
        }

        private void CheckTransaction(Transaction transaction, Money suppliedBudget)
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

            foreach (IContractTransactionValidationLogic internalRule in this.internalRules)
            {
                internalRule.CheckContractTransaction(txData, suppliedBudget);
            }
        }
    }
}
