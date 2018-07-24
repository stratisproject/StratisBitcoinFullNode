using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    /// <inheritdoc />
    [FullValidationRule]
    public sealed class SmartContractCoinviewRule : CoinViewRule
    {
        private List<Transaction> blockTxsProcessed;
        private NBitcoin.Consensus consensusParams;
        private Transaction generatedTransaction;
        private uint refundCounter;
        private SmartContractConsensusRules smartContractParent;

        public SmartContractCoinviewRule()
        {
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            this.Logger.LogTrace("()");

            base.Initialize();

            this.consensusParams = this.Parent.Network.Consensus;
            this.generatedTransaction = null;
            this.refundCounter = 1;
            this.smartContractParent = (SmartContractConsensusRules)this.Parent;

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public async override Task RunAsync(RuleContext context)
        {
            this.Logger.LogTrace("()");

            this.blockTxsProcessed = new List<Transaction>();
            NBitcoin.Block block = context.ValidationContext.Block;
            ChainedHeader index = context.ValidationContext.ChainedHeader;
            DeploymentFlags flags = context.Flags;
            UnspentOutputSet view = ((UtxoRuleContext)context).UnspentOutputSet;

            this.Parent.PerformanceCounter.AddProcessedBlocks(1);

            // Start state from previous block's root
            this.smartContractParent.OriginalStateRoot.SyncToRoot(((SmartContractBlockHeader)context.ConsensusTip.Header).HashStateRoot.ToBytes());
            IContractStateRepository trackedState = this.smartContractParent.OriginalStateRoot.StartTracking();

            this.refundCounter = 1;
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
                        ConsensusErrors.BadBlockSigOps.Throw();

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
                                if (txout.ScriptPubKey.IsSmartContractExec() || txout.ScriptPubKey.IsSmartContractInternalCall())
                                {
                                    return input.ScriptSig.IsSmartContractSpend();
                                }

                                var checker = new TransactionChecker(tx, inputIndexCopy, txout.Value, txData);
                                var ctx = new ScriptEvaluationContext(this.Parent.Network);
                                ctx.ScriptVerify = flags.ScriptFlags;
                                return ctx.VerifyScript(input.ScriptSig, txout.ScriptPubKey, checker);
                            });

                            checkInput.Start();
                            checkInputs.Add(checkInput);
                        }
                    }
                }

                this.UpdateCoinView(context, tx);

                this.blockTxsProcessed.Add(tx);
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

            if (new uint256(this.smartContractParent.OriginalStateRoot.Root) != ((SmartContractBlockHeader)block.Header).HashStateRoot)
                SmartContractConsensusErrors.UnequalStateRoots.Throw();

            this.smartContractParent.OriginalStateRoot.Commit();

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
        public override void CheckBlockReward(RuleContext context, Money fees, int height, NBitcoin.Block block)
        {
            this.Logger.LogTrace("()");

            Money blockReward = fees + this.GetProofOfWorkReward(height);
            if (block.Transactions[0].TotalOut > blockReward)
            {
                this.Logger.LogTrace("(-)[BAD_COINBASE_AMOUNT]");
                ConsensusErrors.BadCoinbaseAmount.Throw();
            }

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
        public override void CheckMaturity(UnspentOutputs coins, int spendHeight)
        {
            base.CheckCoinbaseMaturity(coins, spendHeight);
        }

        /// <inheritdoc />
        /// <remarks>Should someone wish to use POW only we'll need to implement subsidy halving.</remarks>
        public override Money GetProofOfWorkReward(int height)
        {
            if (height == this.Parent.Network.Consensus.PremineHeight)
                return this.Parent.Network.Consensus.PremineReward;

            return this.Parent.Network.Consensus.ProofOfWorkReward;
        }

        /// <summary>
        /// Executes contracts as necessary and updates the coinview / UTXOset after execution.
        /// </summary>
        /// <inheritdoc/>
        public override void UpdateCoinView(RuleContext context, Transaction transaction)
        {
            if (this.generatedTransaction != null)
            {
                ValidateGeneratedTransaction(transaction);
                base.UpdateUTXOSet(context, transaction);
                return;
            }

            // If we are here, was definitely submitted by someone
            ValidateSubmittedTransaction(transaction);

            TxOut smartContractTxOut = transaction.Outputs.FirstOrDefault(txOut => txOut.ScriptPubKey.IsSmartContractExec());
            if (smartContractTxOut == null)
            {
                // Someone submitted a standard transaction - no smart contract opcodes.
                base.UpdateUTXOSet(context, transaction);
                return;
            }

            // Someone submitted a smart contract transaction.
            ExecuteContractTransaction(context, transaction);

            base.UpdateUTXOSet(context, transaction);
        }

        /// <summary>
        /// Validates that any condensing transaction matches the transaction generated during execution
        /// </summary>
        /// <param name="transaction"></param>
        private void ValidateGeneratedTransaction(Transaction transaction)
        {
            if (this.generatedTransaction.GetHash() != transaction.GetHash())
                SmartContractConsensusErrors.UnequalCondensingTx.Throw();

            this.generatedTransaction = null;

            return;
        }

        /// <summary>
        /// Validates that a submitted transacction doesn't contain illegal operations
        /// </summary>
        /// <param name="transaction"></param>
        private void ValidateSubmittedTransaction(Transaction transaction)
        {
            if (transaction.Inputs.Any(x => x.ScriptSig.IsSmartContractSpend()))
                SmartContractConsensusErrors.UserOpSpend.Throw();

            if (transaction.Outputs.Any(x => x.ScriptPubKey.IsSmartContractInternalCall()))
                SmartContractConsensusErrors.UserInternalCall.Throw();
        }

        /// <summary>
        /// Executes the smart contract part of a transaction
        /// </summary>
        private void ExecuteContractTransaction(RuleContext context, Transaction transaction)
        {
            ISmartContractTransactionContext txContext = GetSmartContractTransactionContext(context, transaction);
            ISmartContractExecutor executor = this.smartContractParent.ExecutorFactory.CreateExecutor(this.smartContractParent.OriginalStateRoot, txContext);

            ISmartContractExecutionResult result = executor.Execute(txContext);

            ValidateRefunds(result.Refunds, context.ValidationContext.Block.Transactions[0]);

            if (result.InternalTransaction != null)
                this.generatedTransaction = result.InternalTransaction;

            SaveReceipt(txContext, result);
        }

        /// <summary>
        /// Retrieves the context object to be given to the contract executor.
        /// </summary>
        private ISmartContractTransactionContext GetSmartContractTransactionContext(RuleContext context, Transaction transaction)
        {
            ulong blockHeight = Convert.ToUInt64(context.ValidationContext.ChainedHeader.Height);

            GetSenderUtil.GetSenderResult getSenderResult = GetSenderUtil.GetSender(transaction, this.smartContractParent.UtxoSet, this.blockTxsProcessed);

            if (!getSenderResult.Success)
                throw new ConsensusErrorException(new ConsensusError("sc-consensusvalidator-executecontracttransaction-sender", getSenderResult.Error));

            Script coinbaseScriptPubKey = context.ValidationContext.Block.Transactions[0].Outputs[0].ScriptPubKey;
            GetSenderUtil.GetSenderResult getCoinbaseResult = GetSenderUtil.GetAddressFromScript(coinbaseScriptPubKey);
            if (!getCoinbaseResult.Success)
                throw new ConsensusErrorException(new ConsensusError("sc-consensusvalidator-executecontracttransaction-coinbase", getCoinbaseResult.Error));

            Money mempoolFee = transaction.GetFee(((UtxoRuleContext)context).UnspentOutputSet);

            return new SmartContractTransactionContext(blockHeight, getCoinbaseResult.Sender, mempoolFee, getSenderResult.Sender, transaction);
        }

        /// <summary>
        /// Throws a consensus exception if the gas refund inside the block is different to what this node calculated during execution.
        /// </summary>
        private void ValidateRefunds(List<TxOut> refunds, Transaction coinbaseTransaction)
        {
            this.Logger.LogTrace("({0}:{1})", nameof(refunds), refunds.Count);

            foreach (TxOut refund in refunds)
            {
                TxOut refundToMatch = coinbaseTransaction.Outputs[this.refundCounter];
                if (refund.Value != refundToMatch.Value || refund.ScriptPubKey != refundToMatch.ScriptPubKey)
                {
                    this.Logger.LogTrace("{0}:{1}, {2}:{3}", nameof(refund.Value), refund.Value, nameof(refundToMatch.Value), refundToMatch.Value);
                    this.Logger.LogTrace("{0}:{1}, {2}:{3}", nameof(refund.ScriptPubKey), refund.ScriptPubKey, nameof(refundToMatch.ScriptPubKey), refundToMatch.ScriptPubKey);

                    SmartContractConsensusErrors.UnequalRefundAmounts.Throw();
                }

                this.refundCounter++;
            }

            this.Logger.LogTrace("(-){0}:{1}", nameof(this.refundCounter), this.refundCounter);
        }

        /// <inheritdoc/>
        protected override bool IsProtocolTransaction(Transaction transaction)
        {
            return transaction.IsCoinBase || transaction.IsCoinStake;
        }

        /// <summary>
        /// Saves receipt in a database following execution.
        /// TODO: When we have a receipt root, ensure that this is deterministic, and validated. i.e. block receipt roots match!
        /// TODO: Also put it inside the block assembly then.
        /// </summary>
        private void SaveReceipt(ISmartContractTransactionContext txContext, ISmartContractExecutionResult result)
        {
            // For now we don't want it to interrupt execution so put it in a silly large try catch.
            try
            {
                this.Logger.LogTrace("Save Receipt : {0}:{1}", nameof(txContext.TransactionHash), txContext.TransactionHash);
                this.smartContractParent.ReceiptStorage.SaveReceipt(txContext, result);
            }
            catch (Exception e)
            {
                this.Logger.LogError("Exception occurred saving contract receipt: {0}", e.Message);
            }
        }
    }
}