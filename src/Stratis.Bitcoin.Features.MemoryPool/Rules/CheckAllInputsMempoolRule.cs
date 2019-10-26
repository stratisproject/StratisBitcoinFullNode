using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

namespace Stratis.Bitcoin.Features.MemoryPool.Rules
{
    /// <summary>
    /// Validate inputs against previous transactions.
    /// Checks against <see cref="ScriptVerify.Standard"/> and <see cref="ScriptVerify.P2SH"/>
    /// </summary>
    public class CheckAllInputsMempoolRule : MempoolRule
    {
        private readonly IConsensusRuleEngine consensusRuleEngine;
        private readonly NodeDeployments nodeDeployments;

        public CheckAllInputsMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            IConsensusRuleEngine consensusRuleEngine,
            NodeDeployments nodeDeployments,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
            this.consensusRuleEngine = consensusRuleEngine;
            this.nodeDeployments = nodeDeployments;
        }

        /// <seealso>https://github.com/bitcoin/bitcoin/blob/febf3a856bcfb8fef2cb4ddcb8d1e0cab8a22580/src/validation.cpp#L770</seealso>
        public override void CheckTransaction(MempoolValidationContext context)
        {
            // TODO: How should the RequireStandard setting interact with this?
            var scriptVerifyFlags = ScriptVerify.Standard;

            // Check against previous transactions.
            // This is done last to help prevent CPU exhaustion denial-of-service attacks.
            var txdata = new PrecomputedTransactionData(context.Transaction);
            if (!this.CheckInputs(context, scriptVerifyFlags, txdata))
            {
                // SCRIPT_VERIFY_CLEANSTACK requires SCRIPT_VERIFY_WITNESS, so we
                // need to turn both off, and compare against just turning off CLEANSTACK
                // to see if the failure is specifically due to witness validation.
                if (!context.Transaction.HasWitness && this.CheckInputs(context, scriptVerifyFlags & ~(ScriptVerify.Witness | ScriptVerify.CleanStack), txdata) && !this.CheckInputs(context, scriptVerifyFlags & ~ScriptVerify.CleanStack, txdata))
                {
                    // Only the witness is missing, so the transaction itself may be fine.
                    this.logger.LogTrace("(-)[FAIL_WITNESS_MUTATED]");
                    context.State.Fail(MempoolErrors.WitnessMutated).Throw();
                }

                this.logger.LogTrace("(-)[FAIL_INPUTS_PREV_TXS]");
                context.State.Fail(new MempoolError()).Throw();
            }

            // Check again against just the consensus-critical mandatory script verification flags, in case of bugs in the standard flags that cause
            // transactions to pass as valid when they're actually invalid. For instance the STRICTENC flag was incorrectly allowing certain
            // CHECKSIG NOT scripts to pass, even though they were invalid.
            //
            // There is a similar check during block creation to prevent creating invalid blocks, however allowing such transactions into the mempool
            // can be exploited as a DoS attack.

            DeploymentFlags flags = this.nodeDeployments.GetFlags(this.chainIndexer.Tip);

            if (!this.CheckInputs(context, flags.ScriptFlags, txdata))
            {
                this.logger.LogTrace("(-)[FAIL_SCRIPT_VERIFY]");
                context.State.Fail(new MempoolError(), $"CheckInputs: BUG! PLEASE REPORT THIS! ConnectInputs failed against MANDATORY but not STANDARD flags {context.TransactionHash}").Throw();
            }
        }

        /// <summary>
        /// Validates transaction inputs against transaction data for a specific set of script verify flags.
        /// Check whether all inputs of this transaction are valid (no double spends, scripts & signatures, amounts)
        /// This does not modify the UTXO set.
        /// </summary>
        /// <seealso>https://github.com/bitcoin/bitcoin/blob/febf3a856bcfb8fef2cb4ddcb8d1e0cab8a22580/src/validation.cpp#L1259</seealso>
        /// <param name="ruleContext">Current mempool rule context.</param>
        /// <param name="context">Current validation context.</param>
        /// <param name="scriptVerify">Script verify flag.</param>
        /// <param name="txData">Transaction data.</param>
        /// <returns>Whether inputs are valid.</returns>
        private bool CheckInputs(MempoolValidationContext context, ScriptVerify scriptVerify, PrecomputedTransactionData txData)
        {
            Transaction tx = context.Transaction;

            if (tx.IsCoinBase)
                return true;

            // TODO: The original code does not appear to do these checks here. Reevaluate if this needs to be done, or perhaps moved to another rule/method.
            this.consensusRuleEngine.GetRule<CoinViewRule>().CheckInputs(context.Transaction, context.View.Set, this.chainIndexer.Height + 1);

            // TODO: Original code has the concept of a script execution cache. This might be worth looking into for performance improvements. Signature checks are expensive.

            for (int iInput = 0; iInput < tx.Inputs.Count; iInput++)
            {
                TxIn input = tx.Inputs[iInput];
                int iiInput = iInput;
                TxOut txout = context.View.GetOutputFor(input);

                var checker = new TransactionChecker(tx, iiInput, txout.Value, txData);
                var ctx = new ScriptEvaluationContext(this.network) { ScriptVerify = scriptVerify };
                if (!ctx.VerifyScript(input.ScriptSig, txout.ScriptPubKey, checker))
                {
                    if ((scriptVerify & ScriptVerify.StandardNotMandatory) == ScriptVerify.StandardNotMandatory)
                    {
                        // Check whether the failure was caused by a non-mandatory script verification check, such as
                        // non-standard DER encodings or non-null dummy arguments; if so, don't trigger DoS protection to
                        // avoid splitting the network between upgraded and non-upgraded nodes.

                        // TODO: Investigate whether the checker and context can be reused instead of recreated. Probably not.
                        checker = new TransactionChecker(tx, iiInput, txout.Value, txData);
                        ctx = new ScriptEvaluationContext(this.network) { ScriptVerify = (scriptVerify & ~ScriptVerify.StandardNotMandatory) };

                        if (ctx.VerifyScript(input.ScriptSig, txout.ScriptPubKey, checker))
                        {
                            this.logger.LogTrace("(-)[FAIL_NON_MANDATORY_SCRIPT_VERIFY]");
                            // TODO: Check what this actually means in Core's logic. If it is on testnet/regtest and RequireStandard is false, is the transaction still rejected?
                            context.State.Fail(MempoolErrors.NonMandatoryScriptVerifyFlagFailed, ctx.Error.ToString()).Throw();
                        }
                    }

                    // Failures of other flags indicate a transaction that is invalid in new blocks, e.g. an invalid P2SH. We DoS ban
                    // such nodes as they are not following the protocol. That said, during an upgrade careful thought should be taken
                    // as to the correct behavior - we may want to continue peering with non-upgraded nodes even after soft-fork
                    // super-majority signaling has occurred.

                    // Further comment from Bitcoin Core:
                    // MANDATORY flag failures correspond to
                    // ValidationInvalidReason::CONSENSUS. Because CONSENSUS
                    // failures are the most serious case of validation
                    // failures, we may need to consider using
                    // RECENT_CONSENSUS_CHANGE for any script failure that
                    // could be due to non-upgraded nodes which we may want to
                    // support, to avoid splitting the network (but this
                    // depends on the details of how net_processing handles
                    // such errors).

                    this.logger.LogTrace("(-)[FAIL_MANDATORY_SCRIPT_VERIFY]");
                    context.State.Fail(MempoolErrors.MandatoryScriptVerifyFlagFailed, ctx.Error.ToString()).Throw();
                }
            }

            return true;
        }
    }
}