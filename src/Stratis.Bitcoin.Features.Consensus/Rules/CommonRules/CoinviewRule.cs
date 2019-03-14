using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// The proof of work coinview update rules. Validates the UTXO set is correctly spent and creating new outputs.
    /// </summary>
    /// <exception cref="ConsensusErrors.BadTransactionMissingInput">Thrown if transaction tries to spend inputs that are missing.</exception>
    /// <exception cref="ConsensusErrors.BadTransactionNonFinal">Thrown if transaction's height or time is lower then provided by SequenceLock for this block.</exception>
    /// <exception cref="ConsensusErrors.BadBlockSigOps">Thrown if signature operation cost is greater then maximum block signature operation cost.</exception>
    /// <exception cref="ConsensusErrors.BadTransactionScriptError">Thrown if not all inputs are valid (no double spends, scripts & sigs, amounts).</exception>
    public abstract class CoinViewRule : FullValidationConsensusRule
    {
        /// <summary>Consensus options.</summary>
        public ConsensusOptions ConsensusOptions { get; private set; }

        /// <summary>The consensus.</summary>
        private IConsensus Consensus { get; set; }

        /// <inheritdoc />
        public override void Initialize()
        {
            this.Consensus = this.Parent.Network.Consensus;
            this.ConsensusOptions = this.Parent.Network.Consensus.Options;
        }

        /// <inheritdoc />
        public override async Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;
            ChainedHeader index = context.ValidationContext.ChainedHeaderToValidate;
            DeploymentFlags flags = context.Flags;
            UnspentOutputSet view = (context as UtxoRuleContext).UnspentOutputSet;

            long sigOpsCost = 0;
            Money fees = Money.Zero;
            var inputsToCheck = new List<(Transaction tx, int inputIndexCopy, TxOut txOut, PrecomputedTransactionData txData, TxIn input, DeploymentFlags flags)>();

            for (int txIndex = 0; txIndex < block.Transactions.Count; txIndex++)
            {
                Transaction tx = block.Transactions[txIndex];

                if (!context.SkipValidation)
                {
                    if (!tx.IsCoinBase && !view.HaveInputs(tx))
                    {
                        this.Logger.LogTrace("Transaction '{0}' has not inputs", tx.GetHash());
                        this.Logger.LogTrace("(-)[BAD_TX_NO_INPUT]");
                        ConsensusErrors.BadTransactionMissingInput.Throw();
                    }

                    if (!this.IsTxFinal(tx, context))
                    {
                        this.Logger.LogTrace("Transaction '{0}' is not final", tx.GetHash());
                        this.Logger.LogTrace("(-)[BAD_TX_NON_FINAL]");
                        ConsensusErrors.BadTransactionNonFinal.Throw();
                    }

                    // GetTransactionSignatureOperationCost counts 3 types of sigops:
                    // * legacy (always),
                    // * p2sh (when P2SH enabled in flags and excludes coinbase),
                    // * witness (when witness enabled in flags and excludes coinbase).
                    sigOpsCost += this.GetTransactionSignatureOperationCost(tx, view, flags);
                    if (sigOpsCost > this.ConsensusOptions.MaxBlockSigopsCost)
                    {
                        this.Logger.LogTrace("(-)[BAD_BLOCK_SIG_OPS]");
                        ConsensusErrors.BadBlockSigOps.Throw();
                    }

                    if (!tx.IsCoinBase)
                    {
                        this.CheckInputs(tx, view, index.Height);

                        if (!tx.IsCoinStake)
                            fees += view.GetValueIn(tx) - tx.TotalOut;

                        var txData = new PrecomputedTransactionData(tx);
                        for (int inputIndex = 0; inputIndex < tx.Inputs.Count; inputIndex++)
                        {
                            TxIn input = tx.Inputs[inputIndex];

                            inputsToCheck.Add((
                                tx: tx,
                                inputIndexCopy: inputIndex,
                                txOut: view.GetOutputFor(input),
                                txData,
                                input: input,
                                flags
                            ));
                        }
                    }
                }

                this.UpdateCoinView(context, tx);
            }

            if (!context.SkipValidation)
            {
                this.CheckBlockReward(context, fees, index.Height, block);

                // Start the Parallel loop on a thread so its result can be awaited rather than blocking
                Task<ParallelLoopResult> checkInputsInParallel = Task.Run(() =>
                {
                    return Parallel.ForEach(inputsToCheck, (input, state) =>
                    {
                        if (state.ShouldExitCurrentIteration)
                            return;

                        if (!this.CheckInput(input.tx, input.inputIndexCopy, input.txOut, input.txData, input.input, input.flags))
                        {
                            state.Stop();
                        }
                    });

                });

                ParallelLoopResult loopResult = await checkInputsInParallel.ConfigureAwait(false);

                if (!loopResult.IsCompleted)
                {
                    this.Logger.LogTrace("(-)[BAD_TX_SCRIPT]");

                    ConsensusErrors.BadTransactionScriptError.Throw();
                }
            }
            else this.Logger.LogTrace("BIP68, SigOp cost, and block reward validation skipped for block at height {0}.", index.Height);
        }

        /// <summary>Checks if transaction if final.</summary>
        protected virtual bool IsTxFinal(Transaction transaction, RuleContext context)
        {
            return transaction.IsFinal(context.ValidationContext.ChainedHeaderToValidate);
        }

        /// <summary>
        /// Verify that an input may be validly spent as part of the given transaction in the given block.
        /// </summary>
        /// <param name="tx">Transaction to check.</param>
        /// <param name="inputIndexCopy">Index of the input to check.</param>
        /// <param name="txout">Output the input is spending.</param>
        /// <param name="txData">Transaction data for the transaction being checked.</param>
        /// <param name="input">Input to check.</param>
        /// <param name="flags">Deployment flags</param>
        /// <returns>Whether the input is valid.</returns>
        protected virtual bool CheckInput(Transaction tx, int inputIndexCopy, TxOut txout, PrecomputedTransactionData txData, TxIn input, DeploymentFlags flags)
        {
            var checker = new TransactionChecker(tx, inputIndexCopy, txout.Value, txData);
            var ctx = new ScriptEvaluationContext(this.Parent.Network);
            ctx.ScriptVerify = flags.ScriptFlags;
            bool verifyScriptResult = ctx.VerifyScript(input.ScriptSig, txout.ScriptPubKey, checker);

            if (verifyScriptResult == false)
            {
                this.Logger.LogTrace("Verify script for transaction '{0}' failed, ScriptSig = '{1}', ScriptPubKey = '{2}', script evaluation error = '{3}'", tx.GetHash(), input.ScriptSig, txout.ScriptPubKey, ctx.Error);
            }

            return verifyScriptResult;
        }

        /// <summary>
        /// Update the context's UTXO set.
        /// </summary>
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        /// <param name="transaction">Transaction which outputs will be added to the context's <see cref="UnspentOutputSet"/> and which inputs will be removed from it.</param>
        protected void UpdateUTXOSet(RuleContext context, Transaction transaction)
        {
            ChainedHeader index = context.ValidationContext.ChainedHeaderToValidate;
            UnspentOutputSet view = (context as UtxoRuleContext).UnspentOutputSet;

            view.Update(transaction, index.Height);
        }

        /// <summary>
        /// Network specific updates to the context's UTXO set.
        /// <para>
        /// Refer to <see cref="UpdateUTXOSet(RuleContext, Transaction)"/>.
        /// </para>
        /// </summary>
        public abstract void UpdateCoinView(RuleContext context, Transaction transaction);

        /// <summary>
        /// Verifies that block has correct coinbase transaction with appropriate reward and fees summ.
        /// </summary>
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        /// <param name="fees">Total amount of fees from transactions that are included in that block.</param>
        /// <param name="height">Block's height.</param>
        /// <param name="block">Block for which reward amount is checked.</param>
        /// <exception cref="ConsensusErrors.BadCoinbaseAmount">Thrown if coinbase transaction output value is larger than expected.</exception>
        public abstract void CheckBlockReward(RuleContext context, Money fees, int height, Block block);

        /// <summary>
        /// Checks the maturity of UTXOs.
        /// </summary>
        /// <param name="coins">UTXOs to check the maturity of.</param>
        /// <param name="spendHeight">Height at which coins are attempted to be spent.</param>
        /// <exception cref="ConsensusErrors.BadTransactionPrematureCoinbaseSpending">Thrown if transaction tries to spend coins that are not mature.</exception>
        public void CheckCoinbaseMaturity(UnspentOutputs coins, int spendHeight)
        {
            // If prev is coinbase, check that it's matured
            if (coins.IsCoinbase)
            {
                if ((spendHeight - coins.Height) < this.Consensus.CoinbaseMaturity)
                {
                    this.Logger.LogTrace("Coinbase transaction height {0} spent at height {1}, but maturity is set to {2}.", coins.Height, spendHeight, this.Consensus.CoinbaseMaturity);
                    this.Logger.LogTrace("(-)[COINBASE_PREMATURE_SPENDING]");
                    ConsensusErrors.BadTransactionPrematureCoinbaseSpending.Throw();
                }
            }
        }

        /// <summary>
        /// Network specific logic that checks the maturity of UTXO sets.
        /// <para>
        /// Refer to <see cref="CheckMaturity(UnspentOutputs, int)"/>.
        /// </para>
        /// </summary>
        public abstract void CheckMaturity(UnspentOutputs coins, int spendHeight);

        /// <summary>
        /// Contains checks that need to be performed on each input once UTXO data is available.
        /// </summary>
        /// <param name="transaction">The transaction that is having its input examined.</param>
        /// <param name="coins">The unspent output consumed by the input being examined.</param>
        protected virtual void CheckInputValidity(Transaction transaction, UnspentOutputs coins)
        {
        }

        /// <summary>
        /// Checks that transaction's inputs are valid.
        /// </summary>
        /// <param name="transaction">Transaction to check.</param>
        /// <param name="inputs">Map of previous transactions that have outputs we're spending.</param>
        /// <param name="spendHeight">Height at which we are spending coins.</param>
        /// <exception cref="ConsensusErrors.BadTransactionMissingInput">Thrown if transaction's inputs are missing.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionInputValueOutOfRange">Thrown if input value is out of range.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionInBelowOut">Thrown if transaction inputs are less then outputs.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionNegativeFee">Thrown if fees sum is negative.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionFeeOutOfRange">Thrown if fees value is out of range.</exception>
        public void CheckInputs(Transaction transaction, UnspentOutputSet inputs, int spendHeight)
        {
            if (!inputs.HaveInputs(transaction))
                ConsensusErrors.BadTransactionMissingInput.Throw();

            Money valueIn = Money.Zero;
            Money fees = Money.Zero;
            for (int i = 0; i < transaction.Inputs.Count; i++)
            {
                OutPoint prevout = transaction.Inputs[i].PrevOut;
                UnspentOutputs coins = inputs.AccessCoins(prevout.Hash);

                this.CheckMaturity(coins, spendHeight);

                this.CheckInputValidity(transaction, coins);

                // Check for negative or overflow input values.
                valueIn += coins.TryGetOutput(prevout.N).Value;
                if (!this.MoneyRange(coins.TryGetOutput(prevout.N).Value) || !this.MoneyRange(valueIn))
                {
                    this.Logger.LogTrace("(-)[BAD_TX_INPUT_VALUE]");
                    ConsensusErrors.BadTransactionInputValueOutOfRange.Throw();
                }
            }

            if (!transaction.IsProtocolTransaction())
            {
                if (valueIn < transaction.TotalOut)
                {
                    this.Logger.LogTrace("(-)[TX_IN_BELOW_OUT]");
                    ConsensusErrors.BadTransactionInBelowOut.Throw();
                }

                // Check transaction fees.
                Money txFee = valueIn - transaction.TotalOut;
                if (txFee < 0)
                {
                    this.Logger.LogTrace("(-)[NEGATIVE_FEE]");
                    ConsensusErrors.BadTransactionNegativeFee.Throw();
                }

                fees += txFee;
                if (!this.MoneyRange(fees))
                {
                    this.Logger.LogTrace("(-)[BAD_FEE]");
                    ConsensusErrors.BadTransactionFeeOutOfRange.Throw();
                }
            }
        }

        /// <summary>
        /// Gets the block reward at the provided height.
        /// </summary>
        /// <param name="height">Height of the block that we're calculating the reward for.</param>
        /// <returns>Reward amount.</returns>
        public abstract Money GetProofOfWorkReward(int height);

        /// <summary>
        /// Calculates total signature operation cost of a transaction.
        /// </summary>
        /// <param name="transaction">Transaction for which we are computing the cost.</param>
        /// <param name="inputs">Map of previous transactions that have outputs we're spending.</param>
        /// <param name="flags">Script verification flags.</param>
        /// <returns>Signature operation cost for all transaction's inputs.</returns>
        public long GetTransactionSignatureOperationCost(Transaction transaction, UnspentOutputSet inputs, DeploymentFlags flags)
        {
            long signatureOperationCost = this.GetLegacySignatureOperationsCount(transaction) * this.ConsensusOptions.WitnessScaleFactor;

            if (transaction.IsCoinBase)
                return signatureOperationCost;

            if (flags.ScriptFlags.HasFlag(ScriptVerify.P2SH))
            {
                signatureOperationCost += this.GetP2SHSignatureOperationsCount(transaction, inputs) * this.ConsensusOptions.WitnessScaleFactor;
            }

            for (int i = 0; i < transaction.Inputs.Count; i++)
            {
                TxOut prevout = inputs.GetOutputFor(transaction.Inputs[i]);
                signatureOperationCost += this.CountWitnessSignatureOperation(prevout.ScriptPubKey, transaction.Inputs[i].WitScript, flags);
            }

            return signatureOperationCost;
        }

        /// <summary>
        /// Calculates signature operation cost for single transaction input.
        /// </summary>
        /// <param name="scriptPubKey">Script public key.</param>
        /// <param name="witness">Witness script.</param>
        /// <param name="flags">Script verification flags.</param>
        /// <returns>Signature operation cost for single transaction input.</returns>
        private long CountWitnessSignatureOperation(Script scriptPubKey, WitScript witness, DeploymentFlags flags)
        {
            witness = witness ?? WitScript.Empty;
            if (!flags.ScriptFlags.HasFlag(ScriptVerify.Witness))
                return 0;

            WitProgramParameters witParams = PayToWitTemplate.Instance.ExtractScriptPubKeyParameters2(this.Parent.Network, scriptPubKey);

            if (witParams?.Version == 0)
            {
                if (witParams.Program.Length == 20)
                    return 1;

                if (witParams.Program.Length == 32 && witness.PushCount > 0)
                {
                    Script subscript = Script.FromBytesUnsafe(witness.GetUnsafePush(witness.PushCount - 1));
                    return subscript.GetSigOpCount(true);
                }
            }

            return 0;
        }

        /// <summary>
        /// Calculates pay-to-script-hash (BIP16) transaction signature operation cost.
        /// </summary>
        /// <param name="transaction">Transaction for which we are computing the cost.</param>
        /// <param name="inputs">Map of previous transactions that have outputs we're spending.</param>
        /// <returns>Signature operation cost for transaction.</returns>
        private uint GetP2SHSignatureOperationsCount(Transaction transaction, UnspentOutputSet inputs)
        {
            if (transaction.IsCoinBase)
                return 0;

            uint sigOps = 0;
            for (int i = 0; i < transaction.Inputs.Count; i++)
            {
                TxOut prevout = inputs.GetOutputFor(transaction.Inputs[i]);
                if (prevout.ScriptPubKey.IsPayToScriptHash(this.Parent.Network))
                    sigOps += prevout.ScriptPubKey.GetSigOpCount(this.Parent.Network, transaction.Inputs[i].ScriptSig);
            }

            return sigOps;
        }

        /// <summary>
        /// Calculates legacy transaction signature operation cost.
        /// </summary>
        /// <param name="transaction">Transaction for which we are computing the cost.</param>
        /// <returns>Legacy signature operation cost for transaction.</returns>
        private long GetLegacySignatureOperationsCount(Transaction transaction)
        {
            long sigOps = 0;
            foreach (TxIn txin in transaction.Inputs)
                sigOps += txin.ScriptSig.GetSigOpCount(false);

            foreach (TxOut txout in transaction.Outputs)
                sigOps += txout.ScriptPubKey.GetSigOpCount(false);

            return sigOps;
        }

        /// <summary>
        /// Checks if value is in range from 0 to <see cref="consensusOptions.MaxMoney"/>.
        /// </summary>
        /// <param name="value">The value to be checked.</param>
        /// <returns><c>true</c> if the value is in range. Otherwise <c>false</c>.</returns>
        private bool MoneyRange(long value)
        {
            return ((value >= 0) && (value <= this.Consensus.MaxMoney));
        }

        /// <summary>
        /// Determines whether the block with specified height is premined.
        /// </summary>
        /// <param name="height">Block's height.</param>
        /// <returns><c>true</c> if the block with provided height is premined, <c>false</c> otherwise.</returns>
        protected bool IsPremine(int height)
        {
            return (this.Consensus.PremineHeight > 0) &&
                   (this.Consensus.PremineReward > 0) &&
                   (height == this.Consensus.PremineHeight);
        }
    }
}