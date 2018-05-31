using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractConsensusValidator : PowConsensusValidator
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;
        private readonly ContractStateRepositoryRoot originalStateRoot;
        private readonly CoinView coinView;
        private readonly ISmartContractExecutorFactory executorFactory;
        private readonly ISmartContractReceiptStorage receiptStorage;
        private List<Transaction> blockTxsProcessed;
        private Transaction generatedTransaction;
        private uint refundCounter;

        public SmartContractConsensusValidator(
            CoinView coinView,
            ICheckpoints checkpoints,
            IDateTimeProvider dateTimeProvider,
            ISmartContractExecutorFactory executorFactory,
            ILoggerFactory loggerFactory,
            Network network,
            ContractStateRepositoryRoot stateRoot,
            ISmartContractReceiptStorage receiptStorage)
            : base(network, checkpoints, dateTimeProvider, loggerFactory)
        {
            this.coinView = coinView;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.originalStateRoot = stateRoot;
            this.generatedTransaction = null;
            this.executorFactory = executorFactory;
            this.receiptStorage = receiptStorage;
            this.refundCounter = 1;
        }

        // Same as base, just that it always validates true for scripts for now. Purely for testing.
        public override void ExecuteBlock(RuleContext context, TaskScheduler taskScheduler = null)
        {
            this.logger.LogTrace("()");
            this.blockTxsProcessed = new List<Transaction>();
            NBitcoin.Block block = context.BlockValidationContext.Block;
            ChainedHeader index = context.BlockValidationContext.ChainedHeader;
            DeploymentFlags flags = context.Flags;
            UnspentOutputSet view = context.Set;

            this.PerformanceCounter.AddProcessedBlocks(1);
            taskScheduler = taskScheduler ?? TaskScheduler.Default;

            // Start state from previous block's root
            this.originalStateRoot.SyncToRoot(((SmartContractBlockHeader)context.ConsensusTip.Header).HashStateRoot.ToBytes());
            IContractStateRepository trackedState = this.originalStateRoot.StartTracking();

            this.refundCounter = 1;

            if (!context.SkipValidation)
            {
                if (flags.EnforceBIP30)
                {
                    foreach (Transaction tx in block.Transactions)
                    {
                        UnspentOutputs coins = view.AccessCoins(tx.GetHash());
                        if ((coins != null) && !coins.IsPrunable)
                        {
                            this.logger.LogTrace("(-)[BAD_TX_BIP_30]");
                            ConsensusErrors.BadTransactionBIP30.Throw();
                        }
                    }
                }
            }
            else this.logger.LogTrace("BIP30 validation skipped for checkpointed block at height {0}.", index.Height);

            long sigOpsCost = 0;
            Money fees = Money.Zero;
            var checkInputs = new List<Task<bool>>();
            for (int txIndex = 0; txIndex < block.Transactions.Count; txIndex++)
            {
                this.PerformanceCounter.AddProcessedTransactions(1);
                Transaction tx = block.Transactions[txIndex];
                if (!context.SkipValidation)
                {
                    if (!tx.IsCoinBase && (!context.IsPoS || (context.IsPoS && !tx.IsCoinStake)))
                    {
                        int[] prevheights;

                        if (!view.HaveInputs(tx))
                        {
                            this.logger.LogTrace("(-)[BAD_TX_NO_INPUT]");
                            ConsensusErrors.BadTransactionMissingInput.Throw();
                        }

                        prevheights = new int[tx.Inputs.Count];
                        // Check that transaction is BIP68 final.
                        // BIP68 lock checks (as opposed to nLockTime checks) must
                        // be in ConnectBlock because they require the UTXO set.
                        for (int j = 0; j < tx.Inputs.Count; j++)
                        {
                            prevheights[j] = (int)view.AccessCoins(tx.Inputs[j].PrevOut.Hash).Height;
                        }

                        if (!tx.CheckSequenceLocks(prevheights, index, flags.LockTimeFlags))
                        {
                            this.logger.LogTrace("(-)[BAD_TX_NON_FINAL]");
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

                    // TODO: Simplify this condition.
                    if (!tx.IsCoinBase && (!context.IsPoS || (context.IsPoS && !tx.IsCoinStake)))
                    {
                        this.CheckInputs(tx, view, index.Height);
                        fees += view.GetValueIn(tx) - tx.TotalOut;
                        var txData = new PrecomputedTransactionData(tx);
                        for (int inputIndex = 0; inputIndex < tx.Inputs.Count; inputIndex++)
                        {
                            this.PerformanceCounter.AddProcessedInputs(1);
                            TxIn input = tx.Inputs[inputIndex];
                            int inputIndexCopy = inputIndex;
                            TxOut txout = view.GetOutputFor(input);
                            var checkInput = new Task<bool>(() =>
                            {
                                if (txout.ScriptPubKey.IsSmartContractExec || txout.ScriptPubKey.IsSmartContractInternalCall)
                                {
                                    return input.ScriptSig.IsSmartContractSpend;
                                }

                                var checker = new TransactionChecker(tx, inputIndexCopy, txout.Value, txData);
                                var ctx = new ScriptEvaluationContext(this.network);
                                ctx.ScriptVerify = flags.ScriptFlags;
                                return ctx.VerifyScript(input.ScriptSig, txout.ScriptPubKey, checker);
                            });
                            checkInput.Start(taskScheduler);
                            checkInputs.Add(checkInput);
                        }
                    }
                }

                this.UpdateCoinViewAndExecuteContracts(context, tx, trackedState);
                this.blockTxsProcessed.Add(tx);
            }

            if (!context.SkipValidation)
            {
                this.CheckBlockReward(context, fees, index.Height, block);

                bool passed = checkInputs.All(c => c.GetAwaiter().GetResult());
                if (!passed)
                {
                    this.logger.LogTrace("(-)[BAD_TX_SCRIPT]");
                    ConsensusErrors.BadTransactionScriptError.Throw();
                }
            }
            else this.logger.LogTrace("BIP68, SigOp cost, and block reward validation skipped for block at height {0}.", index.Height);

            if (new uint256(this.originalStateRoot.Root) != ((SmartContractBlockHeader)block.Header).HashStateRoot)
                SmartContractConsensusErrors.UnequalStateRoots.Throw();

            this.originalStateRoot.Commit();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Executes contracts as necessary and updates the coinview / UTXOset after execution.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="transaction"></param>
        protected void UpdateCoinViewAndExecuteContracts(RuleContext context, Transaction transaction, IContractStateRepository trackedState)
        {
            if (this.generatedTransaction != null)
            {
                ValidateGeneratedTransaction(transaction);
                base.UpdateCoinView(context, transaction);
                return;
            }

            // If we are here, was definitely submitted by someone
            ValidateSubmittedTransaction(transaction);
            TxOut smartContractTxOut = transaction.Outputs.FirstOrDefault(txOut => txOut.ScriptPubKey.IsSmartContractExec);
            if (smartContractTxOut == null)
            {
                // Someone submitted a standard transaction - no smart contract opcodes.
                base.UpdateCoinView(context, transaction);
                return;
            }

            // Someone submitted a smart contract transaction.
            ExecuteContractTransaction(context, transaction);
            base.UpdateCoinView(context, transaction);
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
            if (transaction.Inputs.Any(x => x.ScriptSig.IsSmartContractSpend))
                SmartContractConsensusErrors.UserOpSpend.Throw();
            if (transaction.Outputs.Any(x => x.ScriptPubKey.IsSmartContractInternalCall))
                SmartContractConsensusErrors.UserInternalCall.Throw();
        }

        /// <summary>
        /// Executes the smart contract part of a transaction
        /// </summary>
        private void ExecuteContractTransaction(RuleContext context, Transaction transaction)
        {
            ISmartContractTransactionContext txContext = GetSmartContractTransactionContext(context, transaction);
            ISmartContractExecutor executor = this.executorFactory.CreateExecutor(this.originalStateRoot, txContext);

            ISmartContractExecutionResult result = executor.Execute();

            ValidateRefunds(result.Refunds, context.BlockValidationContext.Block.Transactions[0]);

            if (result.InternalTransaction != null)
                this.generatedTransaction = result.InternalTransaction;

            SaveReceipt(txContext, result);
        }

        /// <summary>
        /// Retrieves the context object to be given to the contract executor.
        /// </summary>
        private ISmartContractTransactionContext GetSmartContractTransactionContext(RuleContext context, Transaction transaction)
        {
            ulong blockHeight = Convert.ToUInt64(context.BlockValidationContext.ChainedHeader.Height);

            GetSenderUtil.GetSenderResult getSenderResult = GetSenderUtil.GetSender(this.network, transaction, this.coinView, this.blockTxsProcessed);

            if (!getSenderResult.Success)
            {
                throw new ConsensusErrorException(new ConsensusError("sc-consensusvalidator-executecontracttransaction-sender", getSenderResult.Error));
            }

            Script coinbaseScriptPubKey = context.BlockValidationContext.Block.Transactions[0].Outputs[0].ScriptPubKey;

            GetSenderUtil.GetSenderResult getCoinbaseResult = GetSenderUtil.GetAddressFromScript(this.network, coinbaseScriptPubKey);

            if (!getCoinbaseResult.Success)
            {
                throw new ConsensusErrorException(new ConsensusError("sc-consensusvalidator-executecontracttransaction-coinbase", getCoinbaseResult.Error));
            }

            Money mempoolFee = transaction.GetFee(context.Set);

            return new SmartContractTransactionContext(blockHeight, getCoinbaseResult.Sender, mempoolFee, getSenderResult.Sender, transaction);
        } 

        /// <summary>
        /// Throws a consensus exception if the gas refund inside the block is different to what this node calculated during execution.
        /// </summary>
        private void ValidateRefunds(List<TxOut> refunds, Transaction coinbaseTransaction)
        {
            foreach (TxOut refund in refunds)
            {
                TxOut refundToMatch = coinbaseTransaction.Outputs[this.refundCounter];
                if (refund.Value != refundToMatch.Value || refund.ScriptPubKey != refundToMatch.ScriptPubKey)
                    SmartContractConsensusErrors.UnequalRefundAmounts.Throw();
                this.refundCounter++;
            }
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
                this.logger.LogTrace("Save Receipt : {0}:{1}", nameof(txContext.TransactionHash), txContext.TransactionHash);
                this.receiptStorage.SaveReceipt(txContext, result);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred saving contract receipt: {0}", e.Message);
            }
        }
    }
}