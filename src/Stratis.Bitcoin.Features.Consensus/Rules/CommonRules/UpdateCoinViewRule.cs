using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    [ExecutionRule]
    public class UpdateCoinViewRule : ConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            this.Logger.LogTrace("()");

            Block block = context.BlockValidationContext.Block;
            ChainedBlock index = context.BlockValidationContext.ChainedBlock;
            DeploymentFlags flags = context.Flags;
            UnspentOutputSet unspentOutputSet = context.Set;

           // this.PerformanceCounter.AddProcessedBlocks(1);
           // taskScheduler = taskScheduler ?? TaskScheduler.Default;

            long sigOpsCost = 0;
            Money fees = Money.Zero;
            var checkInputs = new List<Task<bool>>();
            for (int txIndex = 0; txIndex < block.Transactions.Count; txIndex++)
            {
             //   this.PerformanceCounter.AddProcessedTransactions(1);
                Transaction tx = block.Transactions[txIndex];
                if (!context.SkipValidation)
                {

                    if (!tx.IsCoinBase) //&& (!context.IsPoS || (context.IsPoS && !tx.IsCoinStake)))
                    {
                        int[] prevheights;

                        if (!unspentOutputSet.HaveInputs(tx))
                        {
                            this.Logger.LogTrace("(-)[BAD_TX_NO_INPUT]");
                            ConsensusErrors.BadTransactionMissingInput.Throw();
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}