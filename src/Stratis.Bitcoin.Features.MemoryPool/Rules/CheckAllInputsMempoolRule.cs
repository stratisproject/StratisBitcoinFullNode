using Microsoft.Extensions.Logging;
using NBitcoin;
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

        public CheckAllInputsMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            IConsensusRuleEngine consensusRuleEngine,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
            this.consensusRuleEngine = consensusRuleEngine;
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            var scriptVerifyFlags = ScriptVerify.Standard;
            if (!this.settings.RequireStandard)
            {
                // TODO: implement -promiscuousmempoolflags
                // scriptVerifyFlags = GetArg("-promiscuousmempoolflags", scriptVerifyFlags);
            }

            // Check against previous transactions
            // This is done last to help prevent CPU exhaustion denial-of-service attacks.
            var txdata = new PrecomputedTransactionData(context.Transaction);
            if (!this.CheckInputs(context, scriptVerifyFlags, txdata))
            {
                // TODO: Implement Witness Code
                //// SCRIPT_VERIFY_CLEANSTACK requires SCRIPT_VERIFY_WITNESS, so we
                //// need to turn both off, and compare against just turning off CLEANSTACK
                //// to see if the failure is specifically due to witness validation.
                //if (!tx.HasWitness() && CheckInputs(Trx, state, view, true, scriptVerifyFlags & ~(SCRIPT_VERIFY_WITNESS | SCRIPT_VERIFY_CLEANSTACK), true, txdata) &&
                //  !CheckInputs(tx, state, view, true, scriptVerifyFlags & ~SCRIPT_VERIFY_CLEANSTACK, true, txdata))
                //{
                //  // Only the witness is missing, so the transaction itself may be fine.
                //  state.SetCorruptionPossible();
                //}

                this.logger.LogTrace("(-)[FAIL_INPUTS_PREV_TXS]");
                context.State.Fail(new MempoolError()).Throw();
            }

            // Check again against just the consensus-critical mandatory script
            // verification flags, in case of bugs in the standard flags that cause
            // transactions to pass as valid when they're actually invalid. For
            // instance the STRICTENC flag was incorrectly allowing certain
            // CHECKSIG NOT scripts to pass, even though they were invalid.
            //
            // There is a similar check in CreateNewBlock() to prevent creating
            // invalid blocks, however allowing such transactions into the mempool
            // can be exploited as a DoS attack.
            if (!this.CheckInputs(context, ScriptVerify.P2SH, txdata))
            {
                this.logger.LogTrace("(-)[FAIL_SCRIPT_VERIFY]");
                context.State.Fail(new MempoolError(), $"CheckInputs: BUG! PLEASE REPORT THIS! ConnectInputs failed against MANDATORY but not STANDARD flags {context.TransactionHash}").Throw();
            }
        }

        /// <summary>
        /// Validates transaction inputs against transaction data for a specific script verify flag.
        /// Check whether all inputs of this transaction are valid (no double spends, scripts & sigs, amounts)
        /// This does not modify the UTXO set. If pvChecks is not NULL, script checks are pushed onto it
        /// instead of being performed inline.
        /// </summary>
        /// <param name="ruleContext">Current mempool rule context.</param>
        /// <param name="context">Current validation context.</param>
        /// <param name="scriptVerify">Script verify flag.</param>
        /// <param name="txData">Transaction data.</param>
        /// <returns>Whether inputs are valid.</returns>
        private bool CheckInputs(MempoolValidationContext context, ScriptVerify scriptVerify,
            PrecomputedTransactionData txData)
        {
            Transaction tx = context.Transaction;
            if (!context.Transaction.IsCoinBase)
            {
                this.consensusRuleEngine.GetRule<CoinViewRule>().CheckInputs(context.Transaction, context.View.Set, this.chainIndexer.Height + 1);

                for (int iInput = 0; iInput < tx.Inputs.Count; iInput++)
                {
                    TxIn input = tx.Inputs[iInput];
                    int iiIntput = iInput;
                    TxOut txout = context.View.GetOutputFor(input);

                    var checker = new TransactionChecker(tx, iiIntput, txout.Value, txData);
                    var ctx = new ScriptEvaluationContext(this.network);
                    ctx.ScriptVerify = scriptVerify;
                    if (ctx.VerifyScript(input.ScriptSig, txout.ScriptPubKey, checker))
                    {
                        this.logger.LogTrace("(-)[SCRIPT_VERIFIED]:true");
                        return true;
                    }
                    else
                    {
                        //TODO:

                        //if (flags & STANDARD_NOT_MANDATORY_VERIFY_FLAGS)
                        //{
                        //  // Check whether the failure was caused by a
                        //  // non-mandatory script verification check, such as
                        //  // non-standard DER encodings or non-null dummy
                        //  // arguments; if so, don't trigger DoS protection to
                        //  // avoid splitting the network between upgraded and
                        //  // non-upgraded nodes.
                        //  CScriptCheck check2(*coins, tx, i,
                        //          flags & ~STANDARD_NOT_MANDATORY_VERIFY_FLAGS, cacheStore, &txdata);
                        //  if (check2())
                        //      return state.Invalid(false, REJECT_NONSTANDARD, strprintf("non-mandatory-script-verify-flag (%s)", ScriptErrorString(check.GetScriptError())));
                        //}
                        //// Failures of other flags indicate a transaction that is
                        //// invalid in new blocks, e.g. a invalid P2SH. We DoS ban
                        //// such nodes as they are not following the protocol. That
                        //// said during an upgrade careful thought should be taken
                        //// as to the correct behavior - we may want to continue
                        //// peering with non-upgraded nodes even after soft-fork
                        //// super-majority signaling has occurred.
                        this.logger.LogTrace("(-)[FAIL_SCRIPT_VERIFY]");
                        context.State.Fail(MempoolErrors.MandatoryScriptVerifyFlagFailed, ctx.Error.ToString()).Throw();
                    }
                }
            }

            return true;
        }
    }
}