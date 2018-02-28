using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
    /// <summary>
    /// If OP_SPEND, check that the transaction before is a contract call
    /// </summary>
    [ValidationRule(CanSkipValidation = false)]
    public class OpSpendRule : ConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            var block = context.BlockValidationContext.Block;

            var opSpendTransactions = block.Transactions.Where(tx =>
                tx.Outputs.Any(o => o.ScriptPubKey.ToOps().Any(x => x.Code == OpcodeType.OP_SPEND)));
            
            foreach (var opSpendTransaction in opSpendTransactions)
            {
                var thisIndex = block.Transactions.IndexOf(opSpendTransaction);

                if (thisIndex <= 0)
                {
                    this.Throw();
                };

                var previousTransaction = block.Transactions[thisIndex - 1];

                var previousWasOpCall = previousTransaction.Outputs.Any(o =>
                    o.ScriptPubKey.ToOps().Any(op => op.Code == OpcodeType.OP_CALLCONTRACT));

                if (!previousWasOpCall)
                {
                    this.Throw();
                }
            }

            return Task.CompletedTask;
        }

        private void Throw()
        {
            // TODO make nicer
            new ConsensusError("opspend-did-not-follow-opcall",
                "transaction contained an op-spend that did not follow an op-call").Throw();
        }
    }
}
