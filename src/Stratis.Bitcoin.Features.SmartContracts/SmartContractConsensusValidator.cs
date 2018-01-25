using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.ContractValidation;
using Stratis.SmartContracts.State;
using Stratis.SmartContracts.State.AccountAbstractionLayer;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractConsensusValidator : PowConsensusValidator
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly IContractStateRepository state;
        private readonly SmartContractDecompiler smartContractDecompiler;
        private readonly SmartContractValidator smartContractValidator;
        private readonly SmartContractGasInjector smartContractGasInjector;

        public SmartContractConsensusValidator(
            Network network,
            ICheckpoints checkpoints,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            IContractStateRepository state,
            SmartContractDecompiler smartContractDecompiler,
            SmartContractValidator smartContractValidator,
            SmartContractGasInjector smartContractGasInjector)
            : base(network, checkpoints, dateTimeProvider, loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.state = state;
            this.smartContractDecompiler = smartContractDecompiler;
            this.smartContractValidator = smartContractValidator;
            this.smartContractGasInjector = smartContractGasInjector;
        }

        // Same as base, just that it always validates true for scripts for now. Purely for testing.
        public override void ExecuteBlock(RuleContext context, TaskScheduler taskScheduler = null)
        {
            this.logger.LogTrace("()");

            NBitcoin.Block block = context.BlockValidationContext.Block;
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
            foreach (TxOut txOut in transaction.Outputs)
            {
                if (txOut.ScriptPubKey.IsSmartContractExec)
                {
                    var scTransaction = new SmartContractTransaction(txOut, transaction);
                    if (scTransaction.OpCodeType == OpcodeType.OP_CREATECONTRACT)
                    {
                        ExecuteCreateContractTransaction(context, scTransaction);
                    }
                    else if (scTransaction.OpCodeType == OpcodeType.OP_CALLCONTRACT)
                    {
                        ExecuteCallContractTransaction(context, scTransaction);
                    }
                }
            }
        }

        private void ExecuteCreateContractTransaction(RuleContext context, SmartContractTransaction transaction)
        {
            uint160 contractAddress = transaction.GetNewContractAddress(); // TODO: GET ACTUAL NUM
            this.state.CreateAccount(0);
            SmartContractDecompilation decomp = this.smartContractDecompiler.GetModuleDefinition(transaction.ContractCode);
            SmartContractValidationResult validationResult = this.smartContractValidator.ValidateContract(decomp);
            
            if (!validationResult.Valid)
            {
                // expend all of users fee - no deployment
                throw new NotImplementedException();
            }

            this.smartContractGasInjector.AddGasCalculationToContract(decomp.ContractType, decomp.BaseType);
            MemoryStream adjustedCodeMem = new MemoryStream();
            decomp.ModuleDefinition.Write(adjustedCodeMem);
            byte[] adjustedCodeBytes = adjustedCodeMem.ToArray();
            ReflectionVirtualMachine vm = new ReflectionVirtualMachine(this.state);

            MethodDefinition initMethod = decomp.ContractType.Methods.FirstOrDefault(x => x.CustomAttributes.Any(y=>y.AttributeType.FullName == typeof(SmartContractInitAttribute).FullName));
 
            SmartContractExecutionResult result = vm.ExecuteMethod(adjustedCodeMem.ToArray(), new SmartContractExecutionContext {
                BlockNumber = Convert.ToUInt64(context.BlockValidationContext.ChainedBlock.Height),
                Difficulty = Convert.ToUInt64(context.NextWorkRequired.Difficulty),
                CallerAddress = 100, // TODO: FIX THIS
                CallValue = transaction.Value,
                GasLimit = transaction.GasLimit,
                GasPrice = transaction.GasPrice,
                Parameters = transaction.Parameters ?? new object[0],
                CoinbaseAddress = 0, //TODO: FIX THIS
                ContractAddress = contractAddress,
                ContractMethod = initMethod?.Name, // probably better ways of doing this
                ContractTypeName = decomp.ContractType.Name // probably better ways of doing this
            });
            // do something with gas
            
            if (!result.Revert)
            {
                this.state.SetCode(contractAddress, adjustedCodeBytes);
                // anything else to update
            }
        }

        private void ExecuteCallContractTransaction(RuleContext context, SmartContractTransaction transaction)
        {
            byte[] contractCode = this.state.GetCode(transaction.To);
            SmartContractDecompilation decomp = this.smartContractDecompiler.GetModuleDefinition(contractCode); // This is overkill here. Just for testing atm.

            ReflectionVirtualMachine vm = new ReflectionVirtualMachine(this.state);
            SmartContractExecutionResult result = vm.ExecuteMethod(contractCode, new SmartContractExecutionContext
            {
                BlockNumber = Convert.ToUInt64(context.BlockValidationContext.ChainedBlock.Height),
                Difficulty = Convert.ToUInt64(context.NextWorkRequired.Difficulty),
                CallerAddress = 0, // TODO: FIX THIS
                CallValue = transaction.Value,
                GasLimit = transaction.GasLimit,
                GasPrice = transaction.GasPrice,
                Parameters = transaction.Parameters ?? new object[0],
                CoinbaseAddress = 0, //TODO: FIX THIS
                ContractAddress = transaction.To,
                ContractMethod = transaction.MethodName,
                ContractTypeName = decomp.ContractType.Name 
            });

            IList<TransferInfo> transfers = this.state.GetTransfers();
            CondensingTx condensingTx = new CondensingTx(transfers, transaction);
            Transaction newTx = condensingTx.CreateCondensingTx();
            context.BlockValidationContext.Block.AddTransaction(newTx);
        }
    }
}
