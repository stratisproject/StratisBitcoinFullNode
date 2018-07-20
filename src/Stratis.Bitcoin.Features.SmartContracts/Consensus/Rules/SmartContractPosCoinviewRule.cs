using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
    /// <summary>
    /// Proof of stake override for the coinview rules - BIP68, MaxSigOps and BlockReward checks.
    /// </summary>
    [FullValidationRule]
    public sealed class SmartContractPosCoinviewRule : CoinViewRule
    {
        /// <summary>Provides functionality for checking validity of PoS blocks.</summary>
        private IStakeValidator stakeValidator;

        /// <summary>Database of stake related data for the current blockchain.</summary>
        private IStakeChain stakeChain;

        /// <summary>The consensus of the parent Network.</summary>
        private NBitcoin.Consensus consensus;

        private List<Transaction> blockTxsProcessed;
        private Transaction generatedTransaction;
        private uint refundCounter;
        private SmartContractPosRules smartContractParent;

        /// <inheritdoc />
        public override void Initialize()
        {
            this.Logger.LogTrace("()");

            base.Initialize();

            this.consensus = this.Parent.Network.Consensus;
            this.generatedTransaction = null;
            this.refundCounter = 1;
            this.smartContractParent = (SmartContractPosRules)this.Parent;

            this.stakeValidator = this.smartContractParent.StakeValidator;
            this.stakeChain = this.smartContractParent.StakeChain;

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        /// <summary>Compute and store the stake proofs.</summary>
        public override async Task RunAsync(RuleContext context)
        {
            this.Logger.LogTrace("()");

            this.blockTxsProcessed = new List<Transaction>();

            this.CheckAndComputeStake(context);

            await base.RunAsync(context).ConfigureAwait(false);
            var posRuleContext = context as PosRuleContext;
            await this.stakeChain.SetAsync(context.ValidationContext.ChainedHeader, posRuleContext.BlockStake).ConfigureAwait(false);

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
        protected override bool IsProtocolTransaction(Transaction transaction)
        {
            return transaction.IsCoinBase || transaction.IsCoinStake;
        }

        /// <inheritdoc />
        public override void CheckBlockReward(RuleContext context, Money fees, int height, NBitcoin.Block block)
        {
            this.Logger.LogTrace("({0}:{1},{2}:'{3}')", nameof(fees), fees, nameof(height), height);

            if (BlockStake.IsProofOfStake(block))
            {
                var posRuleContext = context as PosRuleContext;
                Money stakeReward = block.Transactions[1].TotalOut - posRuleContext.TotalCoinStakeValueIn;
                Money calcStakeReward = fees + this.GetProofOfStakeReward(height);

                this.Logger.LogTrace("Block stake reward is {0}, calculated reward is {1}.", stakeReward, calcStakeReward);
                if (stakeReward > calcStakeReward)
                {
                    this.Logger.LogTrace("(-)[BAD_COINSTAKE_AMOUNT]");
                    ConsensusErrors.BadCoinstakeAmount.Throw();
                }
            }
            else
            {
                Money blockReward = fees + this.GetProofOfWorkReward(height);
                this.Logger.LogTrace("Block reward is {0}, calculated reward is {1}.", block.Transactions[0].TotalOut, blockReward);
                if (block.Transactions[0].TotalOut > blockReward)
                {
                    this.Logger.LogTrace("(-)[BAD_COINBASE_AMOUNT]");
                    ConsensusErrors.BadCoinbaseAmount.Throw();
                }
            }

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public override void UpdateCoinView(RuleContext context, Transaction transaction)
        {
            this.Logger.LogTrace("()");

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
                var posRuleContext = context as PosRuleContext;
                UnspentOutputSet view = posRuleContext.UnspentOutputSet;
                if (transaction.IsCoinStake)
                    posRuleContext.TotalCoinStakeValueIn = view.GetValueIn(transaction);

                base.UpdateUTXOSet(context, transaction);

                return;
            }

            // Someone submitted a smart contract transaction.
            ExecuteContractTransaction(context, transaction);

            base.UpdateUTXOSet(context, transaction);

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public override void CheckMaturity(UnspentOutputs coins, int spendHeight)
        {
            this.Logger.LogTrace("({0}:'{1}/{2}',{3}:{4})", nameof(coins), coins.TransactionId, coins.Height, nameof(spendHeight), spendHeight);

            base.CheckCoinbaseMaturity(coins, spendHeight);

            if (coins.IsCoinstake)
            {
                if ((spendHeight - coins.Height) < this.consensus.CoinbaseMaturity)
                {
                    this.Logger.LogTrace("Coinstake transaction height {0} spent at height {1}, but maturity is set to {2}.", coins.Height, spendHeight, this.consensus.CoinbaseMaturity);
                    this.Logger.LogTrace("(-)[COINSTAKE_PREMATURE_SPENDING]");
                    ConsensusErrors.BadTransactionPrematureCoinstakeSpending.Throw();
                }
            }

            this.Logger.LogTrace("(-)");
        }

        /// <summary>
        /// Checks and computes stake.
        /// </summary>
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        /// <exception cref="ConsensusErrors.PrevStakeNull">Thrown if previous stake is not found.</exception>
        /// <exception cref="ConsensusErrors.SetStakeEntropyBitFailed">Thrown if failed to set stake entropy bit.</exception>
        private void CheckAndComputeStake(RuleContext context)
        {
            this.Logger.LogTrace("()");

            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeader;
            NBitcoin.Block block = context.ValidationContext.Block;
            var posRuleContext = context as PosRuleContext;
            BlockStake blockStake = posRuleContext.BlockStake;

            // Verify hash target and signature of coinstake tx.
            if (BlockStake.IsProofOfStake(block))
            {
                ChainedHeader prevChainedHeader = chainedHeader.Previous;

                BlockStake prevBlockStake = this.stakeChain.Get(prevChainedHeader.HashBlock);
                if (prevBlockStake == null)
                    ConsensusErrors.PrevStakeNull.Throw();

                // Only do proof of stake validation for blocks that are after the assumevalid block or after the last checkpoint.
                if (!context.SkipValidation)
                {
                    this.stakeValidator.CheckProofOfStake(posRuleContext, prevChainedHeader, prevBlockStake, block.Transactions[1], chainedHeader.Header.Bits.ToCompact());
                }
                else this.Logger.LogTrace("POS validation skipped for block at height {0}.", chainedHeader.Height);
            }

            // PoW is checked in CheckBlock().
            if (BlockStake.IsProofOfWork(block))
                posRuleContext.HashProofOfStake = chainedHeader.Header.GetPoWHash();

            // Compute stake entropy bit for stake modifier.
            if (!blockStake.SetStakeEntropyBit(blockStake.GetStakeEntropyBit()))
            {
                this.Logger.LogTrace("(-)[STAKE_ENTROPY_BIT_FAIL]");
                ConsensusErrors.SetStakeEntropyBitFailed.Throw();
            }

            // Record proof hash value.
            blockStake.HashProof = posRuleContext.HashProofOfStake;

            int lastCheckpointHeight = this.Parent.Checkpoints.GetLastCheckpointHeight();
            if (chainedHeader.Height > lastCheckpointHeight)
            {
                // Compute stake modifier.
                ChainedHeader prevChainedHeader = chainedHeader.Previous;
                BlockStake blockStakePrev = prevChainedHeader == null ? null : this.stakeChain.Get(prevChainedHeader.HashBlock);
                blockStake.StakeModifierV2 = this.stakeValidator.ComputeStakeModifierV2(prevChainedHeader, blockStakePrev, blockStake.IsProofOfWork() ? chainedHeader.HashBlock : blockStake.PrevoutStake.Hash);
            }
            else if (chainedHeader.Height == lastCheckpointHeight)
            {
                // Copy checkpointed stake modifier.
                CheckpointInfo checkpoint = this.Parent.Checkpoints.GetCheckpoint(lastCheckpointHeight);
                blockStake.StakeModifierV2 = checkpoint.StakeModifierV2;
                this.Logger.LogTrace("Last checkpoint stake modifier V2 loaded: '{0}'.", blockStake.StakeModifierV2);
            }
            else this.Logger.LogTrace("POS stake modifier computation skipped for block at height {0} because it is not above last checkpoint block height {1}.", chainedHeader.Height, lastCheckpointHeight);

            this.Logger.LogTrace("(-)[OK]");
        }

        /// <inheritdoc />
        public override Money GetProofOfWorkReward(int height)
        {
            if (this.IsPremine(height))
                return this.consensus.PremineReward;

            return this.consensus.ProofOfWorkReward;
        }

        /// <summary>
        /// Gets miner's coin stake reward.
        /// </summary>
        /// <param name="height">Target block height.</param>
        /// <returns>Miner's coin stake reward.</returns>
        public Money GetProofOfStakeReward(int height)
        {
            if (this.IsPremine(height))
                return this.consensus.PremineReward;

            return this.consensus.ProofOfStakeReward;
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
    }
}