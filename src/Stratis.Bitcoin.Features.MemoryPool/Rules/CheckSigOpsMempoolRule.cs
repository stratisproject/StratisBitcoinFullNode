using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

namespace Stratis.Bitcoin.Features.MemoryPool.Rules
{
    /// <summary>
    /// Check that the transaction doesn't have an excessive number of sigops.
    /// </summary>
    public class CheckSigOpsMempoolRule : IMempoolRule
    {
        public void CheckTransaction(MempoolRuleContext ruleContext, MempoolValidationContext context)
        {
            // Check that the transaction doesn't have an excessive number of
            // sigops, making it impossible to mine. Since the coinbase transaction
            // itself can contain sigops MAX_STANDARD_TX_SIGOPS is less than
            // MAX_BLOCK_SIGOPS; we still consider this an invalid rather than
            // merely non-standard transaction.
            if (context.SigOpsCost > ruleContext.Network.Consensus.Options.MaxStandardTxSigopsCost)
            {
                ruleContext.Logger.LogTrace("(-)[FAIL_TOO_MANY_SIGOPS]");
                context.State.Fail(MempoolErrors.TooManySigops).Throw();
            }
        }
    }
}