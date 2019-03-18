using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts.PoS.Rules
{
    /// <summary>
    /// Proof of stake override for the coinview rules - BIP68, MaxSigOps and BlockReward checks.
    /// </summary>
    public sealed class SmartContractPosCoinviewRule : SmartContractCoinviewRule
    {
        private readonly IConsensus consensus;
        private readonly IStakeChain stakeChain;
        private readonly IStakeValidator stakeValidator;
        
        public SmartContractPosCoinviewRule(Network network, IStateRepositoryRoot stateRepositoryRoot,
            IContractExecutorFactory executorFactory, ICallDataSerializer callDataSerializer,
            ISenderRetriever senderRetriever, IReceiptRepository receiptRepository, ICoinView coinView,
            IStakeChain stakeChain, IStakeValidator stakeValidator) 
            : base(network, stateRepositoryRoot, executorFactory, callDataSerializer, senderRetriever, receiptRepository, coinView)
        {
            this.consensus = network.Consensus;
            this.stakeChain = stakeChain;
            this.stakeValidator = stakeValidator;
        }

        /// <inheritdoc />
        /// <summary>Compute and store the stake proofs.</summary>
        public override async Task RunAsync(RuleContext context)
        {
            this.blockTxsProcessed = new List<Transaction>();
            this.refundCounter = 1;

            this.CheckAndComputeStake(context);

            await base.RunAsync(context);

            await this.stakeChain.SetAsync(context.ValidationContext.ChainedHeaderToValidate, (context as PosRuleContext).BlockStake).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override void CheckBlockReward(RuleContext context, Money fees, int height, NBitcoin.Block block)
        {
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
        }

        /// <inheritdoc />
        public override void UpdateCoinView(RuleContext context, Transaction transaction)
        {
            if (this.generatedTransaction != null)
            {
                base.ValidateGeneratedTransaction(transaction);
                base.UpdateUTXOSet(context, transaction);
                return;
            }

            // If we are here, was definitely submitted by someone
            base.ValidateSubmittedTransaction(transaction);

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
            base.ExecuteContractTransaction(context, transaction);

            base.UpdateUTXOSet(context, transaction);
        }

        /// <inheritdoc />
        public override void CheckMaturity(UnspentOutputs coins, int spendHeight)
        {
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
        }

        /// <summary>
        /// Checks and computes stake.
        /// </summary>
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        /// <exception cref="ConsensusErrors.PrevStakeNull">Thrown if previous stake is not found.</exception>
        /// <exception cref="ConsensusErrors.SetStakeEntropyBitFailed">Thrown if failed to set stake entropy bit.</exception>
        private void CheckAndComputeStake(RuleContext context)
        {
            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeaderToValidate;
            NBitcoin.Block block = context.ValidationContext.BlockToValidate;

            var posRuleContext = context as PosRuleContext;
            if (posRuleContext.BlockStake == null)
                posRuleContext.BlockStake = BlockStake.Load(context.ValidationContext.BlockToValidate);

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
                blockStake.StakeModifierV2 = this.stakeValidator.ComputeStakeModifierV2(prevChainedHeader, blockStakePrev.StakeModifierV2, blockStake.IsProofOfWork() ? chainedHeader.HashBlock : blockStake.PrevoutStake.Hash);
            }
            else if (chainedHeader.Height == lastCheckpointHeight)
            {
                // Copy checkpointed stake modifier.
                CheckpointInfo checkpoint = this.Parent.Checkpoints.GetCheckpoint(lastCheckpointHeight);
                blockStake.StakeModifierV2 = checkpoint.StakeModifierV2;
                this.Logger.LogTrace("Last checkpoint stake modifier V2 loaded: '{0}'.", blockStake.StakeModifierV2);
            }
            else this.Logger.LogTrace("POS stake modifier computation skipped for block at height {0} because it is not above last checkpoint block height {1}.", chainedHeader.Height, lastCheckpointHeight);
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
    }
}