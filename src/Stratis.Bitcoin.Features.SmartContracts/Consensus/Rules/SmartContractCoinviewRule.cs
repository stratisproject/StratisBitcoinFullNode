using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.Consensus;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.Executor.Reflection;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    /// <inheritdoc />
    public abstract class SmartContractCoinviewRule : CoinViewRule
    {
        protected List<Transaction> blockTxsProcessed;
        protected Transaction generatedTransaction;
        protected IList<Receipt> receipts;
        protected uint refundCounter;
        protected IStateRepositoryRoot mutableStateRepository;

        protected ISmartContractCoinviewRule ContractCoinviewRule { get; private set; }

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            this.generatedTransaction = null;
            this.refundCounter = 1;
            this.ContractCoinviewRule = (ISmartContractCoinviewRule)this.Parent;
        }

        /// <inheritdoc />
        public override async Task RunAsync(RuleContext context)
        {
            this.blockTxsProcessed = new List<Transaction>();
            NBitcoin.Block block = context.ValidationContext.BlockToValidate;
            ChainedHeader index = context.ValidationContext.ChainedHeaderToValidate;
            DeploymentFlags flags = context.Flags;
            UnspentOutputSet view = ((UtxoRuleContext)context).UnspentOutputSet;
            
            // Get a IStateRepositoryRoot we can alter without affecting the injected one which is used elsewhere.
            byte[] blockRoot = ((SmartContractBlockHeader)context.ValidationContext.ChainedHeaderToValidate.Previous.Header).HashStateRoot.ToBytes();
            this.mutableStateRepository = this.ContractCoinviewRule.OriginalStateRoot.GetSnapshotTo(blockRoot);

            this.receipts = new List<Receipt>();

            this.refundCounter = 1;
            long sigOpsCost = 0;
            Money fees = Money.Zero;
            var checkInputs = new List<Task<bool>>();

            for (int txIndex = 0; txIndex < block.Transactions.Count; txIndex++)
            {
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

            if (new uint256(this.mutableStateRepository.Root) != ((SmartContractBlockHeader)block.Header).HashStateRoot)
                SmartContractConsensusErrors.UnequalStateRoots.Throw();

            ValidateAndStoreReceipts(((SmartContractBlockHeader)block.Header).ReceiptRoot);

            // Push to underlying database
            this.mutableStateRepository.Commit();

            // Update the globally injected state so all services receive the updates.
            this.ContractCoinviewRule.OriginalStateRoot.SyncToRoot(this.mutableStateRepository.Root);
        }

        /// <inheritdoc/>
        public override void CheckBlockReward(RuleContext context, Money fees, int height, NBitcoin.Block block)
        {
            Money blockReward = fees + this.GetProofOfWorkReward(height);
            if (block.Transactions[0].TotalOut > blockReward)
            {
                this.Logger.LogTrace("(-)[BAD_COINBASE_AMOUNT]");
                ConsensusErrors.BadCoinbaseAmount.Throw();
            }
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
        protected void ValidateGeneratedTransaction(Transaction transaction)
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
        protected void ValidateSubmittedTransaction(Transaction transaction)
        {
            if (transaction.Inputs.Any(x => x.ScriptSig.IsSmartContractSpend()))
                SmartContractConsensusErrors.UserOpSpend.Throw();

            if (transaction.Outputs.Any(x => x.ScriptPubKey.IsSmartContractInternalCall()))
                SmartContractConsensusErrors.UserInternalCall.Throw();
        }

        /// <summary>
        /// Executes the smart contract part of a transaction
        /// </summary>
        protected void ExecuteContractTransaction(RuleContext context, Transaction transaction)
        {
            IContractTransactionContext txContext = GetSmartContractTransactionContext(context, transaction);
            IContractExecutor executor = this.ContractCoinviewRule.ExecutorFactory.CreateExecutor(this.mutableStateRepository, txContext);

            IContractExecutionResult result = executor.Execute(txContext);

            var receipt = new Receipt(
                new uint256(this.mutableStateRepository.Root),
                result.GasConsumed,
                result.Logs.ToArray(),
                txContext.TransactionHash,
                txContext.Sender,
                result.To,
                result.NewContractAddress,
                !result.Revert,
                result.ErrorMessage
            )
            {
                BlockHash = context.ValidationContext.BlockToValidate.GetHash()
            };

            this.receipts.Add(receipt);

            ValidateRefunds(result.Refund, context.ValidationContext.BlockToValidate.Transactions[0]);

            if (result.InternalTransaction != null)
                this.generatedTransaction = result.InternalTransaction;
        }

        /// <summary>
        /// Retrieves the context object to be given to the contract executor.
        /// </summary>
        private IContractTransactionContext GetSmartContractTransactionContext(RuleContext context, Transaction transaction)
        {
            ulong blockHeight = Convert.ToUInt64(context.ValidationContext.ChainedHeaderToValidate.Height);

            GetSenderResult getSenderResult = this.ContractCoinviewRule.SenderRetriever.GetSender(transaction, ((PowConsensusRuleEngine)this.Parent).UtxoSet, this.blockTxsProcessed);

            if (!getSenderResult.Success)
                throw new ConsensusErrorException(new ConsensusError("sc-consensusvalidator-executecontracttransaction-sender", getSenderResult.Error));

            Script coinbaseScriptPubKey = context.ValidationContext.BlockToValidate.Transactions[0].Outputs[0].ScriptPubKey;
            GetSenderResult getCoinbaseResult = this.ContractCoinviewRule.SenderRetriever.GetAddressFromScript(coinbaseScriptPubKey);
            if (!getCoinbaseResult.Success)
                throw new ConsensusErrorException(new ConsensusError("sc-consensusvalidator-executecontracttransaction-coinbase", getCoinbaseResult.Error));

            Money mempoolFee = transaction.GetFee(((UtxoRuleContext)context).UnspentOutputSet);

            return new ContractTransactionContext(blockHeight, getCoinbaseResult.Sender, mempoolFee, getSenderResult.Sender, transaction);
        }

        /// <summary>
        /// Throws a consensus exception if the receipt roots don't match.
        /// </summary>
        private void ValidateAndStoreReceipts(uint256 receiptRoot)
        {
            List<uint256> leaves = this.receipts.Select(x => x.GetHash()).ToList();
            bool mutated = false; // TODO: Do we need this?
            uint256 expectedReceiptRoot = BlockMerkleRootRule.ComputeMerkleRoot(leaves, out mutated);

            if (receiptRoot != expectedReceiptRoot)
                SmartContractConsensusErrors.UnequalReceiptRoots.Throw();

            // TODO: Could also check for equality of logsBloom?

            this.ContractCoinviewRule.ReceiptRepository.Store(this.receipts);
            this.receipts.Clear();
        }

        /// <summary>
        /// Throws a consensus exception if the gas refund inside the block is different to what this node calculated during execution.
        /// </summary>
        private void ValidateRefunds(TxOut refund, Transaction coinbaseTransaction)
        {
            TxOut refundToMatch = coinbaseTransaction.Outputs[this.refundCounter];
            if (refund.Value != refundToMatch.Value || refund.ScriptPubKey != refundToMatch.ScriptPubKey)
            {
                SmartContractConsensusErrors.UnequalRefundAmounts.Throw();
            }

            this.refundCounter++;
        }

        /// <inheritdoc/>
        protected override bool IsProtocolTransaction(Transaction transaction)
        {
            return transaction.IsCoinBase || transaction.IsCoinStake;
        }
    }
}