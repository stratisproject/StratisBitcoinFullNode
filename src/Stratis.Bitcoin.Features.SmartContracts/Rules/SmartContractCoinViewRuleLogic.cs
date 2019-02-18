using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.Rules
{
    /// <summary>
    /// Abstraction for shared SC coinview rule logic. 
    /// TODO: Long-term solution requires refactoring of the FN CoinViewRule implementation.
    /// </summary>
    internal sealed class SmartContractCoinViewRuleLogic
    {
        private readonly List<Transaction> blockTxsProcessed;
        private Transaction generatedTransaction;
        private readonly IList<Receipt> receipts;
        private uint refundCounter;
        private IStateRepositoryRoot mutableStateRepository;

        public SmartContractCoinViewRuleLogic(ConsensusRuleEngine parent)
        {
            this.refundCounter = 1;
            this.Parent = parent;
            this.blockTxsProcessed = new List<Transaction>();
            this.receipts = new List<Receipt>();
            this.ContractCoinviewRule = (ISmartContractCoinviewRule)this.Parent;
        }

        public ISmartContractCoinviewRule ContractCoinviewRule { get; }

        public ConsensusRuleEngine Parent { get; }

        public async Task RunAsync(Func<RuleContext, Task> baseRunAsync, RuleContext context)
        {
            this.blockTxsProcessed.Clear();
            this.receipts.Clear();
            this.refundCounter = 1;
            Block block = context.ValidationContext.BlockToValidate;

            // Get a IStateRepositoryRoot we can alter without affecting the injected one which is used elsewhere.
            byte[] blockRoot = ((ISmartContractBlockHeader)context.ValidationContext.ChainedHeaderToValidate.Previous.Header).HashStateRoot.ToBytes();
            this.mutableStateRepository = this.ContractCoinviewRule.OriginalStateRoot.GetSnapshotTo(blockRoot);

            await baseRunAsync(context);

            var blockHeader = (ISmartContractBlockHeader) block.Header;

            if (new uint256(this.mutableStateRepository.Root) != blockHeader.HashStateRoot)
                SmartContractConsensusErrors.UnequalStateRoots.Throw();

            ValidateAndStoreReceipts(blockHeader.ReceiptRoot);
            ValidateLogsBloom(blockHeader.LogsBloom);

            // Push to underlying database
            this.mutableStateRepository.Commit();

            // Update the globally injected state so all services receive the updates.
            this.ContractCoinviewRule.OriginalStateRoot.SyncToRoot(this.mutableStateRepository.Root);
        }

        /// <summary>
        /// Executes contracts as necessary and updates the coinview / UTXOset after execution.
        /// </summary>
        /// <inheritdoc/>
        public void UpdateCoinView(Action<RuleContext, Transaction> baseUpdateUTXOSet, RuleContext context, Transaction transaction)
        {
            if (this.generatedTransaction != null)
            {
                ValidateGeneratedTransaction(transaction);
                baseUpdateUTXOSet(context, transaction);
                this.blockTxsProcessed.Add(transaction);
                return;
            }

            // If we are here, was definitely submitted by someone
            ValidateSubmittedTransaction(transaction);

            TxOut smartContractTxOut = transaction.Outputs.FirstOrDefault(txOut => SmartContractScript.IsSmartContractExec(txOut.ScriptPubKey));
            if (smartContractTxOut == null)
            {
                // Someone submitted a standard transaction - no smart contract opcodes.
                baseUpdateUTXOSet(context, transaction);
                this.blockTxsProcessed.Add(transaction);
                return;
            }

            // Someone submitted a smart contract transaction.
            ExecuteContractTransaction(context, transaction);

            baseUpdateUTXOSet(context, transaction);
            this.blockTxsProcessed.Add(transaction);
        }

        /// <summary>
        /// Validates that any condensing transaction matches the transaction generated during execution
        /// </summary>
        /// <param name="transaction"></param>
        public void ValidateGeneratedTransaction(Transaction transaction)
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
            IContractTransactionContext txContext = GetSmartContractTransactionContext(context, transaction);
            this.CheckFeeAccountsForGas(txContext.Data, txContext.MempoolFee);
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
                result.Return?.ToString(),
                result.ErrorMessage
            )
            {
                BlockHash = context.ValidationContext.BlockToValidate.GetHash()
            };

            this.receipts.Add(receipt);

            if (result.Refund != null)
            {
                ValidateRefunds(result.Refund, context.ValidationContext.BlockToValidate.Transactions[0]);
            }

            if (result.InternalTransaction != null)
            {
                this.generatedTransaction = result.InternalTransaction;
            }
        }

        /// <summary>
        /// Check that the fee is large enough to account for the potential contract gas usage.
        /// </summary>
        private void CheckFeeAccountsForGas(byte[] callData, Money totalFee)
        {
            // We can trust that deserialisation is successful thanks to SmartContractFormatRule coming before
            Result<ContractTxData> result = this.ContractCoinviewRule.CallDataSerializer.Deserialize(callData);

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

            GetSenderResult getSenderResult = this.ContractCoinviewRule.SenderRetriever.GetSender(transaction, ((PowConsensusRuleEngine)this.Parent).UtxoSet, this.blockTxsProcessed);

            if (!getSenderResult.Success)
                throw new ConsensusErrorException(new ConsensusError("sc-consensusvalidator-executecontracttransaction-sender", getSenderResult.Error));

            Script coinbaseScriptPubKey = context.ValidationContext.BlockToValidate.Transactions[0].Outputs[0].ScriptPubKey;

            GetSenderResult getCoinbaseResult = this.ContractCoinviewRule.SenderRetriever.GetAddressFromScript(coinbaseScriptPubKey);

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

            this.ContractCoinviewRule.ReceiptRepository.Store(this.receipts);
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

        public bool IsProtocolTransaction(Transaction transaction)
        {
            return transaction.IsCoinBase || transaction.IsCoinStake;
        }

        public void Reset()
        {
            this.ResetRefundCounter();
            this.ClearGeneratedTransaction();
        }

        private void ResetRefundCounter()
        {
            this.refundCounter = 1;
        }

        private void ClearGeneratedTransaction()
        {
            this.generatedTransaction = null;
        }
    }
}