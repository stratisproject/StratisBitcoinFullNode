﻿using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Proof of stake override for the coinview rules - BIP68, MaxSigOps and BlockReward checks.
    /// </summary>
    [ExecutionRule]
    public class PosCoinViewRule : PowCoinViewRule
    {
        /// <summary>Provides functionality for checking validity of PoS blocks.</summary>
        private IStakeValidator stakeValidator;

        /// <summary>Database of stake related data for the current blockchain.</summary>
        private IStakeChain stakeChain;

        private PosConsensusOptions posConsensusOptions;

        /// <inheritdoc />
        public override void Initialize()
        {
            this.Logger.LogTrace("()");

            base.Initialize();

            var consensusRules = (PosConsensusRules)this.Parent;

            this.stakeValidator = consensusRules.StakeValidator;
            this.stakeChain = consensusRules.StakeChain;
            this.posConsensusOptions = this.Parent.ConsensusParams.Option<PosConsensusOptions>();

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public override async Task RunAsync(RuleContext context)
        {
            this.Logger.LogTrace("()");

            // Compute and store the stake proofs.
            this.CheckAndComputeStake(context);

            await base.RunAsync(context).ConfigureAwait(false);

            await this.stakeChain.SetAsync(context.BlockValidationContext.ChainedHeader, context.Stake.BlockStake).ConfigureAwait(false);

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        protected override void CheckBlockReward(RuleContext context, Money fees, int height, Block block)
        {
            this.Logger.LogTrace("({0}:{1},{2}:'{3}')", nameof(fees), fees, nameof(height), height);

            if (BlockStake.IsProofOfStake(block))
            {
                Money stakeReward = block.Transactions[1].TotalOut - context.Stake.TotalCoinStakeValueIn;
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
        protected override void UpdateCoinView(RuleContext context, Transaction transaction)
        {
            this.Logger.LogTrace("()");

            UnspentOutputSet view = context.Set;

            if (transaction.IsCoinStake)
                context.Stake.TotalCoinStakeValueIn = view.GetValueIn(transaction);

            base.UpdateCoinView(context, transaction);

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        protected override void CheckMaturity(UnspentOutputs coins, int spendHeight)
        {
            this.Logger.LogTrace("({0}:'{1}/{2}',{3}:{4})", nameof(coins), coins.TransactionId, coins.Height, nameof(spendHeight), spendHeight);

            base.CheckMaturity(coins, spendHeight);

            if (coins.IsCoinstake)
            {
                if ((spendHeight - coins.Height) < this.posConsensusOptions.CoinbaseMaturity)
                {
                    this.Logger.LogTrace("Coinstake transaction height {0} spent at height {1}, but maturity is set to {2}.", coins.Height, spendHeight, this.posConsensusOptions.CoinbaseMaturity);
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

            ChainedHeader chainedHeader = context.BlockValidationContext.ChainedHeader;
            Block block = context.BlockValidationContext.Block;
            BlockStake blockStake = context.Stake.BlockStake;

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
                    this.stakeValidator.CheckProofOfStake(context.Stake, prevChainedHeader, prevBlockStake, block.Transactions[1], chainedHeader.Header.Bits.ToCompact());
                }
                else this.Logger.LogTrace("POS validation skipped for block at height {0}.", chainedHeader.Height);
            }

            // PoW is checked in CheckBlock().
            if (BlockStake.IsProofOfWork(block))
                context.Stake.HashProofOfStake = chainedHeader.Header.GetPoWHash();

            // Compute stake entropy bit for stake modifier.
            if (!blockStake.SetStakeEntropyBit(blockStake.GetStakeEntropyBit()))
            {
                this.Logger.LogTrace("(-)[STAKE_ENTROPY_BIT_FAIL]");
                ConsensusErrors.SetStakeEntropyBitFailed.Throw();
            }

            // Record proof hash value.
            blockStake.HashProof = context.Stake.HashProofOfStake;

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
                return this.posConsensusOptions.PremineReward;

            return this.posConsensusOptions.ProofOfWorkReward;
        }

        /// <summary>
        /// Gets miner's coin stake reward.
        /// </summary>
        /// <param name="height">Target block height.</param>
        /// <returns>Miner's coin stake reward.</returns>
        public Money GetProofOfStakeReward(int height)
        {
            if (this.IsPremine(height))
                return this.posConsensusOptions.PremineReward;

            return this.posConsensusOptions.ProofOfStakeReward;
        }

        /// <summary>
        /// Determines whether the block with specified height is premined.
        /// </summary>
        /// <param name="height">Block's height.</param>
        /// <returns><c>true</c> if the block with provided height is premined, <c>false</c> otherwise.</returns>
        private bool IsPremine(int height)
        {
            return (this.posConsensusOptions.PremineHeight > 0) &&
                   (this.posConsensusOptions.PremineReward > 0) &&
                   (height == this.posConsensusOptions.PremineHeight);
        }
    }
}
