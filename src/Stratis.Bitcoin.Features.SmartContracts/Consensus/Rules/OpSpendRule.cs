using System.Collections.Generic;
using System.Linq;
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
            Block block = context.BlockValidationContext.Block;

            IEnumerable<Transaction> opSpendTransactions = block.Transactions.Where(tx =>
                tx.Inputs.Any(o => o.ScriptSig.ToOps().Any(x => x.Code == OpcodeType.OP_SPEND)));

            foreach (Transaction opSpendTransaction in opSpendTransactions)
            {
                var thisIndex = block.Transactions.IndexOf(opSpendTransaction);

                if (thisIndex <= 0)
                {
                    this.Throw();
                };

                Transaction previousTransaction = block.Transactions[thisIndex - 1];

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
