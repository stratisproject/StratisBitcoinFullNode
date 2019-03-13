using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.Rules
{
    /// <summary>
    /// Checks that smart contract transactions are in a valid format and the data is serialized correctly.
    /// </summary>
    public class ContractTransactionFullValidationRule : FullValidationConsensusRule, ISmartContractMempoolRule
    {
        private readonly ContractTransactionChecker transactionChecker;

        // Keep the rules in a covariant interface.
        private readonly IEnumerable<IContractTransactionFullValidationRule> internalRules;

        public ContractTransactionFullValidationRule(ICallDataSerializer serializer, IEnumerable<IContractTransactionFullValidationRule> internalRules)
        {
            this.transactionChecker = new ContractTransactionChecker(serializer);

            this.internalRules = internalRules;
        }

        public override Task RunAsync(RuleContext context)
        {
            return this.transactionChecker.RunAsync(context, this.internalRules);
        }

        public void CheckTransaction(MempoolValidationContext context)
        {
            this.transactionChecker.CheckTransaction(context, this.internalRules);
        }
    }
}