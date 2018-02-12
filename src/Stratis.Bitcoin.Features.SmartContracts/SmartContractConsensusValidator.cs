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
using Stratis.SmartContracts;
using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.ContractValidation;
using Stratis.SmartContracts.State;
using Stratis.SmartContracts.Util;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractConsensusValidator : PowConsensusValidator
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;
        private IContractStateRepository stateRoot;
        private readonly CoinView coinView;
        private readonly SmartContractDecompiler decompiler;
        private readonly SmartContractValidator validator;
        private readonly SmartContractGasInjector gasInjector;
        private List<Transaction> blockTxsProcessed;
        private Transaction lastProcessed;


        public SmartContractConsensusValidator(
            CoinView coinView,
            Network network,
            ICheckpoints checkpoints,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            IContractStateRepository stateRoot,
            SmartContractDecompiler decompiler,
            SmartContractValidator validator,
            SmartContractGasInjector gasInjector)
            : base(network, checkpoints, dateTimeProvider, loggerFactory)
        {
            this.coinView = coinView;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.stateRoot = stateRoot;
            this.decompiler = decompiler;
            this.validator = validator;
            this.gasInjector = gasInjector;
            this.lastProcessed = null;
        }

        // Same as base, just that it always validates true for scripts for now. Purely for testing.
        public override void ExecuteBlock(RuleContext context, TaskScheduler taskScheduler = null)
        {
            this.logger.LogTrace("()");
            this.blockTxsProcessed = new List<Transaction>();
            NBitcoin.Block block = context.BlockValidationContext.Block;
            ChainedBlock index = context.BlockValidationContext.ChainedBlock;
            DeploymentFlags flags = context.Flags;
            UnspentOutputSet view = context.Set;

            this.PerformanceCounter.AddProcessedBlocks(1);
            taskScheduler = taskScheduler ?? TaskScheduler.Default;

            // Start state from previous block's root
            this.stateRoot = this.stateRoot.GetSnapshotTo(context.ConsensusTip.Header.HashStateRoot.ToBytes());

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

            this.stateRoot.Commit();

            this.logger.LogTrace("(-)");
        }

        protected override void UpdateCoinView(RuleContext context, Transaction transaction)
        {
            base.UpdateCoinView(context, transaction);

            if (this.lastProcessed != null)
            {
                // ensure that transactions generated are equal
                if (this.lastProcessed.GetHash() != transaction.GetHash())
                    throw new Exception("Not matching");
                this.lastProcessed = null;
                return;
            }

            TxOut contractTxOut = transaction.Outputs.FirstOrDefault(txOut => txOut.ScriptPubKey.IsSmartContractExec);
            // boring transaction, return
            if (contractTxOut == null)
                return;

            // if it's a condensing transaction, need to ensure it's identical 

            ulong blockNum = Convert.ToUInt64(context.BlockValidationContext.ChainedBlock.Height);
            ulong difficulty = Convert.ToUInt64(context.NextWorkRequired.Difficulty);

            IContractStateRepository track = this.stateRoot.StartTracking();
            var smartContractCarrier = SmartContractCarrier.Deserialize(transaction, transaction.Outputs[0]);

            smartContractCarrier.Sender = GetSenderUtil.GetSender(transaction, this.coinView, this.blockTxsProcessed);
            Script coinbaseScriptPubKey = context.BlockValidationContext.Block.Transactions[0].Outputs[0].ScriptPubKey;
            uint160 coinbaseAddress = new uint160(coinbaseScriptPubKey.GetDestinationPublicKeys().FirstOrDefault().Hash.ToBytes(), false);

            var executor = new SmartContractTransactionExecutor(track, this.decompiler, this.validator, this.gasInjector, smartContractCarrier, blockNum, difficulty, coinbaseAddress); // TODO: Put coinbase in here
            SmartContractExecutionResult result = executor.Execute();

            if (result.InternalTransactions.Any())
                this.lastProcessed = result.InternalTransactions.FirstOrDefault();

            track.Commit();
        }
    }
}