using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.Caching;
using Stratis.Bitcoin.Features.SmartContracts.PoW;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts.Rules
{
    /// <summary>
    /// Abstraction for shared SC coinview rule logic. 
    /// TODO: Long-term solution requires refactoring of the FN CoinViewRule implementation.
    /// </summary>
    internal sealed class SmartContractCoinViewRuleLogic
    {
        private readonly IStateRepositoryRoot stateRepositoryRoot;
        private readonly IContractExecutorFactory executorFactory;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly ISenderRetriever senderRetriever;
        private readonly IReceiptRepository receiptRepository;
        private readonly ICoinView coinView;
        private readonly IBlockExecutionResultCache executionCache;
        private readonly List<Transaction> blockTxsProcessed;
        private Transaction generatedTransaction;
        private BlockExecutionResultModel cachedResults;
        private readonly IList<Receipt> receipts;
        private uint refundCounter;
        private IStateRepositoryRoot mutableStateRepository;
        private ulong blockGasConsumed;
        private readonly ILogger logger;

        public SmartContractCoinViewRuleLogic(IStateRepositoryRoot stateRepositoryRoot,
            IContractExecutorFactory executorFactory,
            ICallDataSerializer callDataSerializer,
            ISenderRetriever senderRetriever,
            IReceiptRepository receiptRepository,
            ICoinView coinView,
            IBlockExecutionResultCache executionCache,
            ILoggerFactory loggerFactory)
        {
            this.stateRepositoryRoot = stateRepositoryRoot;
            this.executorFactory = executorFactory;
            this.callDataSerializer = callDataSerializer;
            this.senderRetriever = senderRetriever;
            this.receiptRepository = receiptRepository;
            this.coinView = coinView;
            this.executionCache = executionCache;
            this.refundCounter = 1;
            this.blockTxsProcessed = new List<Transaction>();
            this.receipts = new List<Receipt>();
            this.logger = loggerFactory.CreateLogger<SmartContractCoinViewRuleLogic>();
        }

        public async Task RunAsync(Func<RuleContext, Task> baseRunAsync, RuleContext context)
        {
            this.blockTxsProcessed.Clear();
            this.receipts.Clear();
            this.blockGasConsumed = 0;
            this.refundCounter = 1;

            Block block = context.ValidationContext.BlockToValidate;
            this.logger.LogDebug("Block to validate '{0}'", block.GetHash());

            // Get a IStateRepositoryRoot we can alter without affecting the injected one which is used elsewhere.
            uint256 blockRoot = ((ISmartContractBlockHeader)context.ValidationContext.ChainedHeaderToValidate.Previous.Header).HashStateRoot;

            this.logger.LogDebug("Block hash state root '{0}'.", blockRoot);

            this.cachedResults = this.executionCache.GetExecutionResult(block.GetHash());

            if (this.cachedResults == null)
            {
                // We have no cached results. Didn't come from our miner. We execute the contracts, so need to set up a new state repository.
                this.mutableStateRepository = this.stateRepositoryRoot.GetSnapshotTo(blockRoot.ToBytes());
            }
            else
            {
                // We have already done all of this execution when mining so we will use those results.
                this.mutableStateRepository = this.cachedResults.MutatedStateRepository;

                foreach (Receipt receipt in this.cachedResults.Receipts)
                {
                    // Block hash needs to be set for all. It was set during mining and can only be updated after.
                    receipt.BlockHash = block.GetHash();
                    this.receipts.Add(receipt);
                }
            }

            // Always call into the base. When the base class calls back in, we will optionally perform execution based on whether this.cachedResults is set.
            await baseRunAsync(context);

            var blockHeader = (ISmartContractBlockHeader)block.Header;

            var mutableStateRepositoryRoot = new uint256(this.mutableStateRepository.Root);
            uint256 blockHeaderHashStateRoot = blockHeader.HashStateRoot;
            this.logger.LogDebug("Compare state roots '{0}' and '{1}'", mutableStateRepositoryRoot, blockHeaderHashStateRoot);
            if (mutableStateRepositoryRoot != blockHeaderHashStateRoot)
                SmartContractConsensusErrors.UnequalStateRoots.Throw();

            this.ValidateAndStoreReceipts(blockHeader.ReceiptRoot);
            this.ValidateLogsBloom(blockHeader.LogsBloom);

            // Push to underlying database
            this.mutableStateRepository.Commit();

            // Update the globally injected state so all services receive the updates.
            this.stateRepositoryRoot.SyncToRoot(this.mutableStateRepository.Root);
        }

        /// <summary>
        /// Executes contracts as necessary and updates the coinview / UTXOset after execution.
        /// </summary>
        public void UpdateCoinView(Action<RuleContext, Transaction> baseUpdateUTXOSet, RuleContext context, Transaction transaction)
        {
            // We already have results for this block. No need to do any processing other than updating the UTXO set.
            if (this.cachedResults != null)
            {
                baseUpdateUTXOSet(context, transaction);
                this.blockTxsProcessed.Add(transaction);
                return;
            }

            if (this.generatedTransaction != null)
            {
                this.ValidateGeneratedTransaction(transaction);
                baseUpdateUTXOSet(context, transaction);
                this.blockTxsProcessed.Add(transaction);
                return;
            }

            // If we are here, was definitely submitted by someone
            this.ValidateSubmittedTransaction(transaction);

            TxOut smartContractTxOut = transaction.Outputs.FirstOrDefault(txOut => SmartContractScript.IsSmartContractExec(txOut.ScriptPubKey));
            if (smartContractTxOut == null)
            {
                // Someone submitted a standard transaction - no smart contract opcodes.
                baseUpdateUTXOSet(context, transaction);
                this.blockTxsProcessed.Add(transaction);
                return;
            }

            // Someone submitted a smart contract transaction.
            this.ExecuteContractTransaction(context, transaction);

            baseUpdateUTXOSet(context, transaction);
            this.blockTxsProcessed.Add(transaction);
        }

        /// <summary>
        /// Validates that any condensing transaction matches the transaction generated during execution
        /// </summary>
        /// <param name="transaction">The generated transaction to validate.</param>
        public void ValidateGeneratedTransaction(Transaction transaction)
        {
            if (this.generatedTransaction.GetHash() != transaction.GetHash())
                SmartContractConsensusErrors.UnequalCondensingTx.Throw();

            this.generatedTransaction = null;

            return;
        }

        /// <summary>
        /// Validates that a submitted transaction doesn't contain illegal operations.
        /// </summary>
        /// <param name="transaction">The submitted transaction to validate.</param>
        public void ValidateSubmittedTransaction(Transaction transaction)
        {
            if (transaction.Inputs.Any(x => x.ScriptSig.IsSmartContractSpend()))
                SmartContractConsensusErrors.UserOpSpend.Throw();

            if (transaction.Outputs.Any(x => x.ScriptPubKey.IsSmartContractInternalCall()))
                SmartContractConsensusErrors.UserInternalCall.Throw();
        }

        /// <summary>
        /// Throws a consensus exception if the gas refund inside the block is different to what this node calculated during execution.
        /// </summary>
        public void ValidateRefunds(TxOut refund, Transaction coinbaseTransaction)
        {
            // Check that this refund exists before trying to retrieve in case of incorrect block coming in
            if (this.refundCounter >= coinbaseTransaction.Outputs.Count)
                SmartContractConsensusErrors.MissingRefundOutput.Throw();

            TxOut refundToMatch = coinbaseTransaction.Outputs[this.refundCounter];
            if (refund.Value != refundToMatch.Value || refund.ScriptPubKey != refundToMatch.ScriptPubKey)
            {
                SmartContractConsensusErrors.UnequalRefundAmounts.Throw();
            }

            this.refundCounter++;
        }

        /// <summary>
        /// Executes the smart contract part of a transaction
        /// </summary>
        public void ExecuteContractTransaction(RuleContext context, Transaction transaction)
        {
            IContractTransactionContext txContext = this.GetSmartContractTransactionContext(context, transaction);
            this.CheckFeeAccountsForGas(txContext.Data, txContext.MempoolFee);
            IContractExecutor executor = this.executorFactory.CreateExecutor(this.mutableStateRepository, txContext);
            Result<ContractTxData> deserializedCallData = this.callDataSerializer.Deserialize(txContext.Data);

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
                result.Return?.ToString(),
                result.ErrorMessage,
                deserializedCallData.Value.GasPrice,
                txContext.TxOutValue,
                deserializedCallData.Value.IsCreateContract ? null : deserializedCallData.Value.MethodName,
                txContext.BlockHeight)
            {
                BlockHash = context.ValidationContext.BlockToValidate.GetHash()
            };

            this.receipts.Add(receipt);

            if (result.Refund != null)
            {
                this.ValidateRefunds(result.Refund, context.ValidationContext.BlockToValidate.Transactions[0]);
            }

            if (result.InternalTransaction != null)
            {
                this.generatedTransaction = result.InternalTransaction;
            }

            this.CheckBlockGasLimit(result.GasConsumed);
        }

        /// <summary>
        /// Update the total gas expenditure for this block and verify that it has not exceeded the limit.
        /// </summary>
        /// <param name="txGasConsumed">The amount of gas spent executing the smart contract transaction.</param>
        private void CheckBlockGasLimit(ulong txGasConsumed)
        {
            this.blockGasConsumed += txGasConsumed;
            if (this.blockGasConsumed > SmartContractBlockDefinition.GasPerBlockLimit)
                SmartContractConsensusErrors.GasLimitPerBlockExceeded.Throw();
        }

        /// <summary>
        /// Check that the fee is large enough to account for the potential contract gas usage.
        /// </summary>
        private void CheckFeeAccountsForGas(byte[] callData, Money totalFee)
        {
            // We can trust that deserialisation is successful thanks to SmartContractFormatRule coming before
            Result<ContractTxData> result = this.callDataSerializer.Deserialize(callData);

            if (totalFee < new Money(result.Value.GasCostBudget))
            {
                // Supplied satoshis are less than the budget we said we had for the contract execution
                SmartContractConsensusErrors.FeeTooSmallForGas.Throw();
            }
        }

        /// <summary>
        /// Retrieves the context object to be given to the contract executor.
        /// </summary>
        public IContractTransactionContext GetSmartContractTransactionContext(RuleContext context, Transaction transaction)
        {
            ulong blockHeight = Convert.ToUInt64(context.ValidationContext.ChainedHeaderToValidate.Height);

            GetSenderResult getSenderResult = this.senderRetriever.GetSender(transaction, this.coinView, this.blockTxsProcessed);

            if (!getSenderResult.Success)
                throw new ConsensusErrorException(new ConsensusError("sc-consensusvalidator-executecontracttransaction-sender", getSenderResult.Error));

            Script coinbaseScriptPubKey = context.ValidationContext.BlockToValidate.Transactions[0].Outputs[0].ScriptPubKey;

            GetSenderResult getCoinbaseResult = this.senderRetriever.GetAddressFromScript(coinbaseScriptPubKey);

            uint160 coinbaseAddress = (getCoinbaseResult.Success) ? getCoinbaseResult.Sender : uint160.Zero;

            Money mempoolFee = transaction.GetFee(((UtxoRuleContext)context).UnspentOutputSet);

            return new ContractTransactionContext(blockHeight, coinbaseAddress, mempoolFee, getSenderResult.Sender, transaction);
        }

        /// <summary>
        /// Throws a consensus exception if the receipt roots don't match.
        /// </summary>
        public void ValidateAndStoreReceipts(uint256 receiptRoot)
        {
            List<uint256> leaves = this.receipts.Select(x => x.GetHash()).ToList();
            bool mutated = false; // TODO: Do we need this?
            uint256 expectedReceiptRoot = BlockMerkleRootRule.ComputeMerkleRoot(leaves, out mutated);

            if (receiptRoot != expectedReceiptRoot)
                SmartContractConsensusErrors.UnequalReceiptRoots.Throw();

            this.receiptRepository.Store(this.receipts);
        }

        private void ValidateLogsBloom(Bloom blockBloom)
        {
            Bloom logsBloom = new Bloom();

            foreach (Receipt receipt in this.receipts)
            {
                logsBloom.Or(receipt.Bloom);
            }

            if (logsBloom != blockBloom)
                SmartContractConsensusErrors.UnequalLogsBloom.Throw();
        }

        public bool CheckInput(Func<Transaction, int, TxOut, PrecomputedTransactionData, TxIn, DeploymentFlags, bool> baseCheckInput, Transaction tx, int inputIndexCopy, TxOut txout, PrecomputedTransactionData txData, TxIn input, DeploymentFlags flags)
        {
            if (txout.ScriptPubKey.IsSmartContractExec() || txout.ScriptPubKey.IsSmartContractInternalCall())
            {
                return input.ScriptSig.IsSmartContractSpend();
            }

            return baseCheckInput(tx, inputIndexCopy, txout, txData, input, flags);
        }
    }
}
