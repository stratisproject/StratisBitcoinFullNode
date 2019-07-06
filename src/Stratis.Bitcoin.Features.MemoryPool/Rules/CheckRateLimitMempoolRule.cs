using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

namespace Stratis.Bitcoin.Features.MemoryPool.Rules
{
    /// <summary>
    /// Validates the rate limit.
    /// Currently not implemented.
    /// </summary>
    public class CheckRateLimitMempoolRule : IMempoolRule
    {
        public void CheckTransaction(MempoolRuleContext ruleContext, MempoolValidationContext context)
        {
            // Whether to limit free transactions:
            // context.State.LimitFree
        }
    }
}