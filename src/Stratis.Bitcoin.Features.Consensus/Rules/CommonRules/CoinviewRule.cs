﻿using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
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
            this.Logger.LogTrace("()");

            this.Consensus = this.Parent.Network.Consensus;
            this.ConsensusOptions = this.Parent.Network.Consensus.Options;

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public override async Task RunAsync(RuleContext context)
        {
            this.Logger.LogTrace("()");

            Block block = context.ValidationContext.BlockToValidate;
            ChainedHeader index = context.ValidationContext.ChainedHeaderToValidate;
            DeploymentFlags flags = context.Flags;
            UnspentOutputSet view = (context as UtxoRuleContext).UnspentOutputSet;

            this.Parent.PerformanceCounter.AddProcessedBlocks(1);

            long sigOpsCost = 0;
            Money fees = Money.Zero;
            var checkInputs = new List<Task<bool>>();
            for (int txIndex = 0; txIndex < block.Transactions.Count; txIndex++)
            {
                this.Parent.PerformanceCounter.AddProcessedTransactions(1);
                Transaction tx = block.Transactions[txIndex];
                if (!context.SkipValidation)
                {
                    if (!this.IsProtocolTransaction(tx))
                    {
                        if (!view.HaveInputs(tx))
                        {
                            this.Logger.LogTrace("(-)[BAD_TX_NO_INPUT]");
                            ConsensusErrors.BadTransactionMissingInput.Throw();
                        }

                        var prevheights = new int[tx.Inputs.Count];
                        // Check that transaction is BIP68 final.
                        // BIP68 lock checks (as opposed to nLockTime checks) must
                        // be in ConnectBlock because they require the UTXO set.
                        for (int j = 0; j < tx.Inputs.Count; j++)
                        {
                            prevheights[j] = (int)view.AccessCoins(tx.Inputs[j].PrevOut.Hash).Height;
                        }

                        if (!tx.CheckSequenceLocks(prevheights, index, flags.LockTimeFlags))
                        {
                            this.Logger.LogTrace("(-)[BAD_TX_NON_FINAL]");
                            ConsensusErrors.BadTransactionNonFinal.Throw();
                        }
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

                    if (!this.IsProtocolTransaction(tx))
                    {
                        this.CheckInputs(tx, view, index.Height);
                        fees += view.GetValueIn(tx) - tx.TotalOut;
                        var txData = new PrecomputedTransactionData(tx);
                        for (int inputIndex = 0; inputIndex < tx.Inputs.Count; inputIndex++)
                        {
                            this.Parent.PerformanceCounter.AddProcessedInputs(1);
                            TxIn input = tx.Inputs[inputIndex];
                            int inputIndexCopy = inputIndex;
                            TxOut txout = view.GetOutputFor(input);
                            var checkInput = new Task<bool>(() =>
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
                            });
                            checkInput.Start();
                            checkInputs.Add(checkInput);
                        }
                    }
                }

                this.UpdateCoinView(context, tx);
            }

            if (!context.SkipValidation)
            {
                this.CheckBlockReward(context, fees, index.Height, block);

                foreach (Task<bool> checkInput in checkInputs)
                {
                    if (await checkInput.ConfigureAwait(false))
                        continue;

                    this.Logger.LogTrace("(-)[BAD_TX_SCRIPT]");
                    ConsensusErrors.BadTransactionScriptError.Throw();
                }
            }
            else this.Logger.LogTrace("BIP68, SigOp cost, and block reward validation skipped for block at height {0}.", index.Height);

            this.Logger.LogTrace("(-)");
        }

        /// <summary>
        /// Update the context's UTXO set.
        /// </summary>
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        /// <param name="transaction">Transaction which outputs will be added to the context's <see cref="UnspentOutputSet"/> and which inputs will be removed from it.</param>
        protected void UpdateUTXOSet(RuleContext context, Transaction transaction)
        {
            this.Logger.LogTrace("()");

            ChainedHeader index = context.ValidationContext.ChainedHeaderToValidate;
            UnspentOutputSet view = (context as UtxoRuleContext).UnspentOutputSet;

            view.Update(transaction, index.Height);

            this.Logger.LogTrace("(-)");
        }

        /// <summary>
        /// Check whether the transaction is part of the protocol (Coinbase or Coinstake).
        /// </summary>
        /// <param name="transaction">The transaction to check.</param>
        protected abstract bool IsProtocolTransaction(Transaction transaction);

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
            this.Logger.LogTrace("({0}:'{1}/{2}',{3}:{4})", nameof(coins), coins.TransactionId, coins.Height, nameof(spendHeight), spendHeight);

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

            this.Logger.LogTrace("(-)");
        }

        /// <summary>
        /// Network specific logic that checks the maturity of UTXO sets.
        /// <para>
        /// Refer to <see cref="CheckMaturity(UnspentOutputs, int)"/>.
        /// </para>
        /// </summary>
        public abstract void CheckMaturity(UnspentOutputs coins, int spendHeight);

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
            this.Logger.LogTrace("({0}:{1})", nameof(spendHeight), spendHeight);

            if (!inputs.HaveInputs(transaction))
                ConsensusErrors.BadTransactionMissingInput.Throw();

            Money valueIn = Money.Zero;
            Money fees = Money.Zero;
            for (int i = 0; i < transaction.Inputs.Count; i++)
            {
                OutPoint prevout = transaction.Inputs[i].PrevOut;
                UnspentOutputs coins = inputs.AccessCoins(prevout.Hash);

                this.CheckMaturity(coins, spendHeight);

                // Check for negative or overflow input values.
                valueIn += coins.TryGetOutput(prevout.N).Value;
                if (!this.MoneyRange(coins.TryGetOutput(prevout.N).Value) || !this.MoneyRange(valueIn))
                {
                    this.Logger.LogTrace("(-)[BAD_TX_INPUT_VALUE]");
                    ConsensusErrors.BadTransactionInputValueOutOfRange.Throw();
                }
            }

            if (valueIn < transaction.TotalOut)
            {
                this.Logger.LogTrace("(-)[TX_IN_BELOW_OUT]");
                ConsensusErrors.BadTransactionInBelowOut.Throw();
            }

            // Tally transaction fees.
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

            this.Logger.LogTrace("(-)");
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
        /// Gets the block weight.
        /// </summary>
        /// <remarks>
        /// This implements the <c>weight = (stripped_size * 4) + witness_size</c> formula, using only serialization with and without witness data.
        /// As witness_size is equal to total_size - stripped_size, this formula is identical to: <c>weight = (stripped_size * 3) + total_size</c>.
        /// </remarks>
        /// <param name="block">Block that we get weight of.</param>
        /// <returns>Block weight.</returns>
        /// TODO: this is a duplicate of the same method in BlockSizeRule <see cref="BlockSizeRule.GetBlockWeight"/>
        public long GetBlockWeight(Block block)
        {
            return this.GetSize(block, TransactionOptions.None)
                   * (this.ConsensusOptions.WitnessScaleFactor - 1)
                   + this.GetSize(block, TransactionOptions.Witness);
        }

        /// <summary>
        /// Gets serialized size of <paramref name="data"/> in bytes.
        /// </summary>
        /// <param name="data">Data that we calculate serialized size of.</param>
        /// <param name="options">Serialization options.</param>
        /// <returns>Serialized size of <paramref name="data"/> in bytes.</returns>
        /// TODO: this is a duplicate of the same method in BlockSizeRule <see cref="BlockSizeRule.GetSize"/>
        private int GetSize(IBitcoinSerializable data, TransactionOptions options)
        {
            var bms = new BitcoinStream(Stream.Null, true);
            bms.TransactionOptions = options;
            bms.ConsensusFactory = this.Parent.Network.Consensus.ConsensusFactory;
            data.ReadWrite(bms);
            return (int)bms.Counter.WrittenBytes;
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
