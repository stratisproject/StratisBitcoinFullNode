using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.MemoryPool.Rules
{
    // TODO: This is still a large rule, perhaps further split it up according to the public methods

    /// <summary>
    /// Creates a memory pool entry in the validation context.
    /// Validates the transactions can be mined, and the pay to script hashs are standard.
    /// Calculates the fees related to the transaction.
    /// </summary>
    public class CreateMempoolEntryMempoolRule : MempoolRule
    {
        public const int WitnessV0ScriptHashSize = 32;
        public const int MaxStandardP2wshScriptSize = 3600;
        public const int MaxStandardP2wshStackItems = 100;
        public const int MaxStandardP2wshStackItemSize = 80;

        private readonly IConsensusRuleEngine consensusRules;

        public CreateMempoolEntryMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            IConsensusRuleEngine consensusRules,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
            this.consensusRules = consensusRules;
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            // Only accept BIP68 sequence locked transactions that can be mined in the next
            // block; we don't want our mempool filled up with transactions that can't
            // be mined yet.
            // Must keep pool.cs for this unless we change CheckSequenceLocks to take a
            // CoinsViewCache instead of create its own
            if (!CheckSequenceLocks(this.network, this.chainIndexer.Tip, context, MempoolValidator.StandardLocktimeVerifyFlags, context.LockPoints))
            {
                this.logger.LogTrace("(-)[FAIL_BIP68_SEQLOCK]");
                context.State.Fail(MempoolErrors.NonBIP68Final).Throw();
            }

            // Check for non-standard pay-to-script-hash in inputs
            if (this.settings.RequireStandard && !this.AreInputsStandard(this.network, context.Transaction, context.View))
            {
                this.logger.LogTrace("(-)[INVALID_NONSTANDARD_INPUTS]");
                context.State.Invalid(MempoolErrors.NonstandardInputs).Throw();
            }

            // Check for non-standard witness in P2WSH
            if (context.Transaction.HasWitness && this.settings.RequireStandard && !this.IsWitnessStandard(context.Transaction, context.View))
            {
                this.logger.LogTrace("(-)[INVALID_NONSTANDARD_WITNESS]");
                context.State.Invalid(MempoolErrors.NonstandardWitness).Throw();
            }

            context.SigOpsCost = this.consensusRules.GetRule<CoinViewRule>().GetTransactionSignatureOperationCost(context.Transaction, context.View.Set, new DeploymentFlags { ScriptFlags = ScriptVerify.Standard });

            Money nValueIn = context.View.GetValueIn(context.Transaction);

            context.ValueOut = context.Transaction.TotalOut;
            context.Fees = nValueIn - context.ValueOut;
            // nModifiedFees includes any fee deltas from PrioritiseTransaction
            Money nModifiedFees = context.Fees;
            double priorityDummy = 0;
            this.mempool.ApplyDeltas(context.TransactionHash, ref priorityDummy, ref nModifiedFees);
            context.ModifiedFees = nModifiedFees;

            (double dPriority, Money inChainInputValue) = context.View.GetPriority(context.Transaction, this.chainIndexer.Height);

            // Keep track of transactions that spend a coinbase, which we re-scan
            // during reorgs to ensure COINBASE_MATURITY is still met.
            bool spendsCoinbase = context.View.SpendsCoinBase(context.Transaction);

            context.Entry = new TxMempoolEntry(context.Transaction, context.Fees, context.State.AcceptTime, dPriority, this.chainIndexer.Height, inChainInputValue,
                spendsCoinbase, context.SigOpsCost, context.LockPoints, this.network.Consensus.Options);
            context.EntrySize = (int)context.Entry.GetTxSize();
        }

        /// <summary>
        /// Check if transaction will be BIP 68 final in the next block to be created.
        /// Simulates calling SequenceLocks() with data from the tip of the current active chain.
        /// Optionally stores in LockPoints the resulting height and time calculated and the hash
        /// of the block needed for calculation or skips the calculation and uses the LockPoints
        /// passed in for evaluation.
        /// The LockPoints should not be considered valid if CheckSequenceLocks returns false.
        /// See consensus/consensus.h for flag definitions.
        /// </summary>
        /// <param name="network">The blockchain network.</param>
        /// <param name="tip">Tip of the chain.</param>
        /// <param name="context">Validation context for the memory pool.</param>
        /// <param name="flags">Transaction lock time flags.</param>
        /// <param name="lp">Optional- existing lock points to use, and update during evaluation.</param>
        /// <param name="useExistingLockPoints">Whether to use the existing lock points during evaluation.</param>
        /// <returns>Whether sequence lock validated.</returns>
        /// <seealso cref="SequenceLock.Evaluate(ChainedHeader)"/>
        public static bool CheckSequenceLocks(Network network, ChainedHeader tip, MempoolValidationContext context, Transaction.LockTimeFlags flags, LockPoints lp = null, bool useExistingLockPoints = false)
        {
            Block dummyBlock = network.Consensus.ConsensusFactory.CreateBlock();
            dummyBlock.Header.HashPrevBlock = tip.HashBlock;
            var index = new ChainedHeader(dummyBlock.Header, dummyBlock.GetHash(), tip);

            // CheckSequenceLocks() uses chainActive.Height()+1 to evaluate
            // height based locks because when SequenceLocks() is called within
            // ConnectBlock(), the height of the block *being*
            // evaluated is what is used.
            // Thus if we want to know if a transaction can be part of the
            // *next* block, we need to use one more than chainActive.Height()

            SequenceLock lockPair;
            if (useExistingLockPoints)
            {
                Guard.Assert(lp != null);
                lockPair = new SequenceLock(lp.Height, lp.Time);
            }
            else
            {
                // pcoinsTip contains the UTXO set for chainActive.Tip()
                var prevheights = new List<int>();
                foreach (TxIn txin in context.Transaction.Inputs)
                {
                    UnspentOutputs coins = context.View.GetCoins(txin.PrevOut.Hash);
                    if (coins == null)
                        return false;

                    if (coins.Height == TxMempool.MempoolHeight)
                    {
                        // Assume all mempool transaction confirm in the next block
                        prevheights.Add(tip.Height + 1);
                    }
                    else
                    {
                        prevheights.Add((int)coins.Height);
                    }
                }
                lockPair = context.Transaction.CalculateSequenceLocks(prevheights.ToArray(), index, flags);

                if (lp != null)
                {
                    lp.Height = lockPair.MinHeight;
                    lp.Time = lockPair.MinTime.ToUnixTimeMilliseconds();
                    // Also store the hash of the block with the highest height of
                    // all the blocks which have sequence locked prevouts.
                    // This hash needs to still be on the chain
                    // for these LockPoint calculations to be valid
                    // Note: It is impossible to correctly calculate a maxInputBlock
                    // if any of the sequence locked inputs depend on unconfirmed txs,
                    // except in the special case where the relative lock time/height
                    // is 0, which is equivalent to no sequence lock. Since we assume
                    // input height of tip+1 for mempool txs and test the resulting
                    // lockPair from CalculateSequenceLocks against tip+1.  We know
                    // EvaluateSequenceLocks will fail if there was a non-zero sequence
                    // lock on a mempool input, so we can use the return value of
                    // CheckSequenceLocks to indicate the LockPoints validity
                    int maxInputHeight = 0;
                    foreach (int height in prevheights)
                    {
                        // Can ignore mempool inputs since we'll fail if they had non-zero locks
                        if (height != tip.Height + 1)
                        {
                            maxInputHeight = Math.Max(maxInputHeight, height);
                        }
                    }

                    lp.MaxInputBlock = tip.GetAncestor(maxInputHeight);
                }
            }

            return lockPair.Evaluate(index);
        }

        /// <summary>
        /// Whether transaction inputs are standard.
        /// Check for standard transaction types.
        /// </summary>
        /// <param name="tx">Transaction to verify.</param>
        /// <param name="mapInputs">Map of previous transactions that have outputs we're spending.</param>
        /// <returns>Whether all inputs (scriptSigs) use only standard transaction forms.</returns>
        private bool AreInputsStandard(Network network, Transaction tx, MempoolCoinView mapInputs)
        {
            if (tx.IsCoinBase)
            {
                this.logger.LogTrace("(-)[IS_COINBASE]:true");
                return true; // Coinbases don't use vin normally
            }

            foreach (TxIn txin in tx.Inputs)
            {
                TxOut prev = mapInputs.GetOutputFor(txin);
                ScriptTemplate template = network.StandardScriptsRegistry.GetTemplateFromScriptPubKey(prev.ScriptPubKey);
                if (template == null)
                {
                    this.logger.LogTrace("(-)[BAD_SCRIPT_TEMPLATE]:false");
                    return false;
                }

                if (template.Type == TxOutType.TX_SCRIPTHASH)
                {
                    if (prev.ScriptPubKey.GetSigOpCount(true) > 15) //MAX_P2SH_SIGOPS
                    {
                        this.logger.LogTrace("(-)[SIG_OP_MAX]:false");
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Whether transaction is witness standard.
        /// <seealso cref="https://github.com/bitcoin/bitcoin/blob/aa624b61c928295c27ffbb4d27be582f5aa31b56/src/policy/policy.cpp#L196"/>
        /// </summary>
        /// <param name="tx">Transaction to verify.</param>
        /// <param name="mapInputs">Map of previous transactions that have outputs we're spending.</param>
        /// <returns>Whether transaction is witness standard.</returns>
        private bool IsWitnessStandard(Transaction tx, MempoolCoinView mapInputs)
        {
            if (tx.IsCoinBase)
            {
                this.logger.LogTrace("(-)[IS_COINBASE]:true");
                return true; // Coinbases are skipped.
            }

            foreach (TxIn input in tx.Inputs)
            {
                // We don't care if witness for this input is empty, since it must not be bloated.
                // If the script is invalid without witness, it would be caught sooner or later during validation.
                if (input.WitScript == null)
                    continue;

                TxOut prev = mapInputs.GetOutputFor(input);

                // Get the scriptPubKey corresponding to this input.
                Script prevScript = prev.ScriptPubKey;
                if (prevScript.IsPayToScriptHash(this.network))
                {
                    // If the scriptPubKey is P2SH, we try to extract the redeemScript casually by converting the scriptSig
                    // into a stack. We do not check IsPushOnly nor compare the hash as these will be done later anyway.
                    // If the check fails at this stage, we know that this txid must be a bad one.
                    PayToScriptHashSigParameters sigParams = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(this.network, input.ScriptSig);
                    if (sigParams == null || sigParams.RedeemScript == null)
                    {
                        this.logger.LogTrace("(-)[BAD_TXID]:false");
                        return false;
                    }

                    prevScript = sigParams.RedeemScript;
                }

                // Non-witness program must not be associated with any witness.
                if (!prevScript.IsWitness(this.network))
                {
                    this.logger.LogTrace("(-)[WITNESS_MISMATCH]:false");
                    return false;
                }

                // Check P2WSH standard limits.
                WitProgramParameters wit = PayToWitTemplate.Instance.ExtractScriptPubKeyParameters2(this.chainIndexer.Network, prevScript);
                if (wit == null)
                {
                    this.logger.LogTrace("(-)[BAD_WITNESS_PARAMS]:false");
                    return false;
                }

                // Version 0 segregated witness program validation.
                if (wit.Version == 0 && wit.Program.Length == WitnessV0ScriptHashSize)
                {
                    WitScript witness = input.WitScript;

                    // Get P2WSH script from top of stack.
                    Script scriptPubKey = Script.FromBytesUnsafe(witness.GetUnsafePush(witness.PushCount - 1));

                    // Stack items are remainder of stack.
                    int sizeWitnessStack = witness.PushCount - 1;

                    // Get the witness stack items.
                    var stack = new List<byte[]>();
                    for (int i = 0; i < sizeWitnessStack; i++)
                    {
                        stack.Add(witness.GetUnsafePush(i));
                    }

                    // Validate P2WSH script isn't larger than max length.
                    if (scriptPubKey.ToBytes(true).Length > MaxStandardP2wshScriptSize)
                    {
                        this.logger.LogTrace("(-)[P2WSH_SCRIPT_SIZE]:false");
                        return false;
                    }

                    // Validate number items in witness stack isn't larger than max.
                    if (sizeWitnessStack > MaxStandardP2wshStackItems)
                    {
                        this.logger.LogTrace("(-)[P2WSH_STACK_ITEMS]:false");
                        return false;
                    }

                    // Validate size of each of the witness stack items.
                    for (int j = 0; j < sizeWitnessStack; j++)
                    {
                        if (stack[j].Length > MaxStandardP2wshStackItemSize)
                        {
                            this.logger.LogTrace("(-)[P2WSH_STACK_ITEM_SIZE]:false");
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}