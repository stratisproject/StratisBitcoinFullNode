using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.State;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SCConsensusValidator : PowConsensusValidator
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly ISmartContractStateRepository state;

        public SCConsensusValidator(
            Network network,
            ICheckpoints checkpoints,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            ISmartContractStateRepository state)
            : base(network, checkpoints, dateTimeProvider, loggerFactory)
        {
            this.state = state;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override void ExecuteBlock(RuleContext context, TaskScheduler taskScheduler = null)
        {
            this.logger.LogTrace("()");

            Block block = context.BlockValidationContext.Block;
            ChainedBlock index = context.BlockValidationContext.ChainedBlock;
            DeploymentFlags flags = context.Flags;
            UnspentOutputSet view = context.Set;

            this.PerformanceCounter.AddProcessedBlocks(1);
            taskScheduler = taskScheduler ?? TaskScheduler.Default;

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
                                return true; // TODO: OBVIOUSLY DON'T DO THIS
                                var checker = new TransactionChecker(tx, inputIndexCopy, txout.Value, txData);
                                var ctx = new ScriptEvaluationContext();
                                ctx.ScriptVerify = flags.ScriptFlags;
                                return ctx.VerifyScript(input.ScriptSig, txout.ScriptPubKey, checker);
                            });
                            checkInput.Start(taskScheduler);
                            checkInputs.Add(checkInput);
                        }
                    }
                }

                this.UpdateCoinView(context, tx);
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

            this.logger.LogTrace("(-)");
        }

        protected override void UpdateCoinView(RuleContext context, Transaction transaction)
        {
            // check if it is a boring transaction and can be handled normally
            if (!transaction.Outputs.Any(x => x.ScriptPubKey.IsSmartContractExec))
            {
                base.UpdateCoinView(context, transaction);
                return;
            }

            // Need to update balances for these transactions

            foreach (var txOut in transaction.Outputs)
            {
                if (txOut.ScriptPubKey.IsSmartContractExec)
                {
                    var scTransaction = new SCTransaction(txOut);
                    if (scTransaction.OpCodeType == OpcodeType.OP_CREATECONTRACT)
                    {
                        ExecuteCreateContractTransaction(scTransaction);
                    }
                    else if (scTransaction.OpCodeType == OpcodeType.OP_CALLCONTRACT)
                    {
                        ExecuteCallContractTransaction(scTransaction);
                    }
                }
            }
        }

        // TODO: These should only be updating a kind of temporary store

        // Real database should only be updated on block being accepted.

        private void ExecuteCreateContractTransaction(SCTransaction transaction)
        {
            // decompile code to module
            // validate module with analyzer
            // inject gasspend

            // run method with reflectionvirtualmachine

            // gas?? add to coinbase transaction?
            // save code to database
            throw new NotImplementedException();
        }

        private void ExecuteCallContractTransaction(SCTransaction transaction)
        {
            // get code from db

            // run method with reflectionvirtualmachine

            // gas?? add to coinbase transaction?

            throw new NotImplementedException();
        }
    }
}
