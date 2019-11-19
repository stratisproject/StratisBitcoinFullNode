using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
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

        /// <summary>
        /// Executes the set of <see cref="IContractTransactionValidationRule"/> rules.
        /// </summary>
        public Task RunAsync(RuleContext context, IEnumerable<IContractTransactionValidationRule> rules)
        {
            Block block = context.ValidationContext.BlockToValidate;

            var contractTransactionValidationRules = rules.ToList();

            foreach (Transaction transaction in block.Transactions)
            {
                this.CheckTransaction(transaction, contractTransactionValidationRules);
            }

            return Task.CompletedTask;
        }
        
        public static ContractTxData GetContractTxData(ICallDataSerializer callDataSerializer, TxOut scTxOut)
        {
            Result<ContractTxData> callDataDeserializationResult = callDataSerializer.Deserialize(scTxOut.ScriptPubKey.ToBytes());

            if (callDataDeserializationResult.IsFailure)
            {
                new ConsensusError("invalid-calldata-format", string.Format("Invalid {0} format", typeof(ContractTxData).Name)).Throw();
            }

            ContractTxData txData = callDataDeserializationResult.Value;

            return txData;
        }

        private void CheckTransaction(Transaction transaction, IEnumerable<IContractTransactionValidationRule> rules)
        {
            TxOut scTxOut = transaction.TryGetSmartContractTxOut();

            if (scTxOut == null)
            {
                // No SC output to validate.
                return;
            }

            ContractTxData txData = GetContractTxData(this.callDataSerializer, scTxOut);

            foreach (IContractTransactionValidationRule rule in rules)
            {
                rule.CheckContractTransaction(txData, null);
            }
        }
    }
}