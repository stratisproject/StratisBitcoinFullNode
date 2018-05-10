using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// Provides functionality for verifying validity of PoS block.
    /// </summary>
    /// <remarks>
    /// These are the criteria for a new block to be accepted as a valid POS block at version 3 of the protocol,
    /// which has been active since 6 August 2016 07:03:21 (Unix epoch time > 1470467000). All timestamps
    /// are Unix epoch timestamps with seconds precision.
    /// <list type="bullet">
    /// <item>New block's timestamp ('BlockTime') MUST be strictly greater than previous block's timestamp.</item>
    /// <item>Coinbase transaction's (first transaction in the block with no inputs) timestamp MUST be inside interval ['BlockTime' - 15; 'BlockTime'].</item>
    /// <item>Coinstake transaction's (second transaction in the block with at least one input and at least 2 outputs and first output being empty) timestamp
    /// MUST be equal to 'BlockTime' and it MUST have lower 4 bits set to 0 (i.e. be divisible by 16) - see <see cref="StakeTimestampMask"/>.</item>
    /// <item>Block's header 'nBits' field MUST be set to the correct POS target value.</item>
    /// <item>All transactions in the block must be final, which means their 'nLockTime' is either zero, or it is lower than current block's height
    /// or node's 'AdjustedTime'. 'AdjustedTime' is the synchronized time among the node and its peers.</item>
    /// <item>Coinstake transaction MUST be signed correctly.</item>
    /// <item>Coinstake transaction's kernel (first) input MUST not be created within last <see cref="PosConsensusOptions.StakeMinConfirmations"/> blocks,
    /// i.e. it MUST have that many confirmation at least.</item>
    /// <item>Coinstake transaction's kernel must meet the staking target using this formula:
    /// <code>hash(stakeModifierV2 + stakingCoins.Time + prevout.Hash + prevout.N + transactionTime) &lt; target * weight</code>
    /// <para>
    /// where 'stakingCoins' is the coinstake's kernel UTXO, 'prevout' is the kernel's output in that transaction,
    /// 'prevout.Hash' is the hash of that transaction; 'transactionTime' is coinstake's transaction time; 'target' is the target as
    /// in 'Bits' block header; 'weight' is the value of the kernel's input.
    /// </para>
    /// </item>
    /// <item>Block's height MUST NOT be more than 500 blocks back - i.e. reorganizations longer than 500 are not allowed.</item>
    /// <item>Coinbase 'scriptSig' starts with serialized block height value. This means that coinbase transaction commits to the height of the block it appears in.</item>
    /// </list>
    /// </remarks>
    public class PosConsensusValidator : PowConsensusValidator, IPosConsensusValidator
    {
        /// <summary>PoS block's timestamp mask.</summary>
        /// <remarks>Used to decrease granularity of timestamp. Supposed to be 2^n-1.</remarks>
        public const uint StakeTimestampMask = 0x0000000F;

        /// <summary>Drifting Bug Fix, hardfork on Sat, 19 Nov 2016 00:00:00 GMT.</summary>
        public const long DriftingBugFixTimestamp = 1479513600;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provides functionality for checking validity of PoS blocks.</summary>
        public IStakeValidator StakeValidator { get; }

        /// <summary>Database of stake related data for the current blockchain.</summary>
        private readonly IStakeChain stakeChain;

        /// <summary>Proof of Stake consensus options.</summary>
        private readonly PosConsensusOptions consensusOptions;

        /// <inheritdoc />
        /// <param name="stakeValidator">Provides functionality for checking validity of PoS blocks.</param>
        /// <param name="checkpoints">Provider of block header hash checkpoints.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="stakeChain">Database of stake related data for the current blockchain.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public PosConsensusValidator(
            IStakeValidator stakeValidator,
            ICheckpoints checkpoints,
            Network network,
            IStakeChain stakeChain,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory)
            : base(network, checkpoints, dateTimeProvider, loggerFactory)
        {
            Guard.NotNull(network.Consensus.Option<PosConsensusOptions>(), nameof(network.Consensus.Options));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.StakeValidator = stakeValidator;
            this.stakeChain = stakeChain;
            this.consensusOptions = network.Consensus.Option<PosConsensusOptions>();
        }

        /// <inheritdoc />
        protected override void CheckBlockReward(RuleContext context, Money fees, int height, Block block)
        {
            this.logger.LogTrace("({0}:{1},{2}:'{3}')", nameof(fees), fees, nameof(height), height);

            if (BlockStake.IsProofOfStake(block))
            {
                Money stakeReward = block.Transactions[1].TotalOut - context.Stake.TotalCoinStakeValueIn;
                Money calcStakeReward = fees + this.GetProofOfStakeReward(height);

                this.logger.LogTrace("Block stake reward is {0}, calculated reward is {1}.", stakeReward, calcStakeReward);
                if (stakeReward > calcStakeReward)
                {
                    this.logger.LogTrace("(-)[BAD_COINSTAKE_AMOUNT]");
                    ConsensusErrors.BadCoinstakeAmount.Throw();
                }
            }
            else
            {
                Money blockReward = fees + this.GetProofOfWorkReward(height);
                this.logger.LogTrace("Block reward is {0}, calculated reward is {1}.", block.Transactions[0].TotalOut, blockReward);
                if (block.Transactions[0].TotalOut > blockReward)
                {
                    this.logger.LogTrace("(-)[BAD_COINBASE_AMOUNT]");
                    ConsensusErrors.BadCoinbaseAmount.Throw();
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public override void ExecuteBlock(RuleContext context, TaskScheduler taskScheduler = null)
        {
            this.logger.LogTrace("()");

            // Compute and store the stake proofs.
            this.CheckAndComputeStake(context);

            base.ExecuteBlock(context, taskScheduler);

            this.stakeChain.Set(context.BlockValidationContext.ChainedHeader, context.Stake.BlockStake);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        protected override void UpdateCoinView(RuleContext context, Transaction transaction)
        {
            this.logger.LogTrace("()");

            UnspentOutputSet view = context.Set;

            if (transaction.IsCoinStake)
                context.Stake.TotalCoinStakeValueIn = view.GetValueIn(transaction);

            base.UpdateCoinView(context, transaction);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        protected override void CheckMaturity(UnspentOutputs coins, int spendHeight)
        {
            this.logger.LogTrace("({0}:'{1}/{2}',{3}:{4})", nameof(coins), coins.TransactionId, coins.Height, nameof(spendHeight), spendHeight);

            base.CheckMaturity(coins, spendHeight);

            if (coins.IsCoinstake)
            {
                if ((spendHeight - coins.Height) < this.consensusOptions.CoinbaseMaturity)
                {
                    this.logger.LogTrace("Coinstake transaction height {0} spent at height {1}, but maturity is set to {2}.", coins.Height, spendHeight, this.consensusOptions.CoinbaseMaturity);
                    this.logger.LogTrace("(-)[COINSTAKE_PREMATURE_SPENDING]");
                    ConsensusErrors.BadTransactionPrematureCoinstakeSpending.Throw();
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Checks and computes stake.
        /// </summary>
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        /// <exception cref="ConsensusErrors.PrevStakeNull">Thrown if previous stake is not found.</exception>
        /// <exception cref="ConsensusErrors.SetStakeEntropyBitFailed">Thrown if failed to set stake entropy bit.</exception>
        private void CheckAndComputeStake(RuleContext context)
        {
            this.logger.LogTrace("()");

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
                    this.StakeValidator.CheckProofOfStake(context.Stake, prevChainedHeader, prevBlockStake, block.Transactions[1], chainedHeader.Header.Bits.ToCompact());
                }
                else this.logger.LogTrace("POS validation skipped for block at height {0}.", chainedHeader.Height);
            }

            // PoW is checked in CheckBlock().
            if (BlockStake.IsProofOfWork(block))
                context.Stake.HashProofOfStake = chainedHeader.Header.GetPoWHash();

            // Compute stake entropy bit for stake modifier.
            if (!blockStake.SetStakeEntropyBit(blockStake.GetStakeEntropyBit()))
            {
                this.logger.LogTrace("(-)[STAKE_ENTROPY_BIT_FAIL]");
                ConsensusErrors.SetStakeEntropyBitFailed.Throw();
            }

            // Record proof hash value.
            blockStake.HashProof = context.Stake.HashProofOfStake;

            int lastCheckpointHeight = this.Checkpoints.GetLastCheckpointHeight();
            if (chainedHeader.Height > lastCheckpointHeight)
            {
                // Compute stake modifier.
                ChainedHeader prevChainedHeader = chainedHeader.Previous;
                BlockStake blockStakePrev = prevChainedHeader == null ? null : this.stakeChain.Get(prevChainedHeader.HashBlock);
                blockStake.StakeModifierV2 = this.StakeValidator.ComputeStakeModifierV2(prevChainedHeader, blockStakePrev, blockStake.IsProofOfWork() ? chainedHeader.HashBlock : blockStake.PrevoutStake.Hash);
            }
            else if (chainedHeader.Height == lastCheckpointHeight)
            {
                // Copy checkpointed stake modifier.
                CheckpointInfo checkpoint = this.Checkpoints.GetCheckpoint(lastCheckpointHeight);
                blockStake.StakeModifierV2 = checkpoint.StakeModifierV2;
                this.logger.LogTrace("Last checkpoint stake modifier V2 loaded: '{0}'.", blockStake.StakeModifierV2);
            }
            else this.logger.LogTrace("POS stake modifier computation skipped for block at height {0} because it is not above last checkpoint block height {1}.", chainedHeader.Height, lastCheckpointHeight);

            this.logger.LogTrace("(-)[OK]");
        }

        /// <inheritdoc />
        public override Money GetProofOfWorkReward(int height)
        {
            if (this.IsPremine(height))
                return this.consensusOptions.PremineReward;

            return this.consensusOptions.ProofOfWorkReward;
        }

        /// <inheritdoc />
        public Money GetProofOfStakeReward(int height)
        {
            if (this.IsPremine(height))
                return this.consensusOptions.PremineReward;

            return this.consensusOptions.ProofOfStakeReward;
        }

        /// <summary>
        /// Determines whether the block with specified height is premined.
        /// </summary>
        /// <param name="height">Block's height.</param>
        /// <returns><c>true</c> if the block with provided height is premined, <c>false</c> otherwise.</returns>
        private bool IsPremine(int height)
        {
            return (this.consensusOptions.PremineHeight > 0) &&
                   (this.consensusOptions.PremineReward > 0) &&
                   (height == this.consensusOptions.PremineHeight);
        }
    }
}
