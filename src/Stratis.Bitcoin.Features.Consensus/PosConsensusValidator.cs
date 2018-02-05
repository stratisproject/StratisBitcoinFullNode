﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;
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
        private readonly StakeChain stakeChain;

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
            StakeChain stakeChain,
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

            // TODO: A temporary fix till this methods is fixed in NStratis.
            (this.stakeChain as StakeChainStore).Set(context.BlockValidationContext.ChainedBlock, context.Stake.BlockStake);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public override void CheckBlock(RuleContext context)
        {
            this.logger.LogTrace("()");

            base.CheckBlock(context);

            Block block = context.BlockValidationContext.Block;

            // Check timestamp.
            if (block.Header.Time > this.FutureDrift(this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp()))
            {
                // The block can be valid only after its time minus the future drift.
                context.BlockValidationContext.RejectUntil = Utils.UnixTimeToDateTime(block.Header.Time - this.FutureDrift(0)).UtcDateTime;
                this.logger.LogTrace("(-)[TIME_TOO_FAR]");
                ConsensusErrors.BlockTimestampTooFar.Throw();
            }

            if (BlockStake.IsProofOfStake(block))
            {
                // Coinbase output should be empty if proof-of-stake block.
                if ((block.Transactions[0].Outputs.Count != 1) || !block.Transactions[0].Outputs[0].IsEmpty)
                {
                    this.logger.LogTrace("(-)[COINBASE_NOT_EMPTY]");
                    ConsensusErrors.BadStakeBlock.Throw();
                }

                // Second transaction must be coinstake, the rest must not be.
                if (!block.Transactions[1].IsCoinStake)
                {
                    this.logger.LogTrace("(-)[NO_COINSTAKE]");
                    ConsensusErrors.BadStakeBlock.Throw();
                }

                if (block.Transactions.Skip(2).Any(t => t.IsCoinStake))
                {
                    this.logger.LogTrace("(-)[MULTIPLE_COINSTAKE]");
                    ConsensusErrors.BadMultipleCoinstake.Throw();
                }
            }

            // Check proof-of-stake block signature.
            if (!this.CheckBlockSignature(block))
            {
                this.logger.LogTrace("(-)[BAD_SIGNATURE]");
                ConsensusErrors.BadBlockSignature.Throw();
            }

            // Check transactions.
            foreach (Transaction transaction in block.Transactions)
            {
                // Check transaction timestamp.
                if (block.Header.Time < transaction.Time)
                {
                    this.logger.LogTrace("Block contains transaction with timestamp {0}, which is greater than block's timestamp {1}.", transaction.Time, block.Header.Time);
                    this.logger.LogTrace("(-)[TX_TIME_MISMATCH]");
                    ConsensusErrors.BlockTimeBeforeTrx.Throw();
                }
            }

            this.logger.LogTrace("(-)[OK]");
        }

        /// <inheritdoc />
        public override void CheckTransaction(Transaction transaction)
        {
            this.logger.LogTrace("()");

            base.CheckTransaction(transaction);

            foreach (TxOut txout in transaction.Outputs)
            {
                if (txout.IsEmpty && !transaction.IsCoinBase && !transaction.IsCoinStake)
                {
                    this.logger.LogTrace("(-)[USER_TXOUT_EMPTY]");
                    ConsensusErrors.BadTransactionEmptyOutput.Throw();
                }
            }

            this.logger.LogTrace("(-)[OK]");
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

        /// <inheritdoc />
        public override void ContextualCheckBlock(RuleContext context)
        {
            this.logger.LogTrace("()");

            base.ContextualCheckBlock(context);

            // TODO: fix this validation code

            //// check proof-of-stake
            //// Limited duplicity on stake: prevents block flood attack
            //// Duplicate stake allowed only when there is orphan child block
            //if (!fReindex && !fImporting && pblock->IsProofOfStake() && setStakeSeen.count(pblock->GetProofOfStake()) && !mapOrphanBlocksByPrev.count(hash))
            //    return error("ProcessBlock() : duplicate proof-of-stake (%s, %d) for block %s", pblock->GetProofOfStake().first.ToString(), pblock->GetProofOfStake().second, hash.ToString());

            //if (!BlockValidator.IsCanonicalBlockSignature(context.BlockResult.Block, false))
            //{
            //    //if (node != null && (int)node.Version >= CANONICAL_BLOCK_SIG_VERSION)
            //    //node.Misbehaving(100);

            //    //return false; //error("ProcessBlock(): bad block signature encoding");
            //}

            //if (!BlockValidator.IsCanonicalBlockSignature(context.BlockResult.Block, true))
            //{
            //    //if (pfrom && pfrom->nVersion >= CANONICAL_BLOCK_SIG_LOW_S_VERSION)
            //    //{
            //    //    pfrom->Misbehaving(100);
            //    //    return error("ProcessBlock(): bad block signature encoding (low-s)");
            //    //}

            //    if (!BlockValidator.EnsureLowS(context.BlockResult.Block.BlockSignatur))
            //        return false; // error("ProcessBlock(): EnsureLowS failed");
            //}

            this.logger.LogTrace("(-)[OK]");
        }

        /// <inheritdoc />
        public override void ContextualCheckBlockHeader(RuleContext context)
        {
            this.logger.LogTrace("()");
            base.ContextualCheckBlockHeader(context);

            ChainedBlock chainedBlock = context.BlockValidationContext.ChainedBlock;
            this.logger.LogTrace("Height of block is {0}, block timestamp is {1}, previous block timestamp is {2}, block version is 0x{3:x}.", chainedBlock.Height, chainedBlock.Header.Time, chainedBlock.Previous.Header.Time, chainedBlock.Header.Version);

            if (chainedBlock.Header.Version < 7)
            {
                this.logger.LogTrace("(-)[BAD_VERSION]");
                ConsensusErrors.BadVersion.Throw();
            }

            if (context.Stake.BlockStake.IsProofOfWork() && (chainedBlock.Height > this.ConsensusParams.LastPOWBlock))
            {
                this.logger.LogTrace("(-)[POW_TOO_HIGH]");
                ConsensusErrors.ProofOfWorkTooHeigh.Throw();
            }

            // Check coinbase timestamp.
            if (chainedBlock.Header.Time > this.FutureDrift(context.BlockValidationContext.Block.Transactions[0].Time))
            {
                this.logger.LogTrace("(-)[TIME_TOO_NEW]");
                ConsensusErrors.TimeTooNew.Throw();
            }

            // Check coinstake timestamp.
            if (context.Stake.BlockStake.IsProofOfStake()
                && !this.CheckCoinStakeTimestamp(chainedBlock.Header.Time, context.BlockValidationContext.Block.Transactions[1].Time))
            {
                this.logger.LogTrace("(-)[BAD_TIME]");
                ConsensusErrors.StakeTimeViolation.Throw();
            }

            // Check timestamp against prev.
            if (chainedBlock.Header.Time <= chainedBlock.Previous.Header.Time)
            {
                this.logger.LogTrace("(-)[TIME_TOO_EARLY]");
                ConsensusErrors.BlockTimestampTooEarly.Throw();
            }

            this.logger.LogTrace("(-)[OK]");
        }

        /// <summary>
        /// Checks whether the coinstake timestamp meets protocol.
        /// </summary>
        /// <param name="blockTime">The block time.</param>
        /// <param name="transactionTime">Transaction UNIX timestamp.</param>
        /// <returns><c>true</c> if block timestamp is equal to transaction timestamp, <c>false</c> otherwise.</returns>
        private bool CheckCoinStakeTimestamp(long blockTime, long transactionTime)
        {
            return (blockTime == transactionTime) && ((transactionTime & StakeTimestampMask) == 0);
        }

        /// <summary>
        /// Checks whether the future drift should be reduced after provided timestamp.
        /// </summary>
        /// <param name="time">UNIX timestamp.</param>
        /// <returns><c>true</c> if for this timestamp future drift should be reduced, <c>false</c> otherwise.</returns>
        private bool IsDriftReduced(long time)
        {
            return time > DriftingBugFixTimestamp;
        }

        /// <summary>
        /// Applies future drift to provided timestamp.
        /// </summary>
        /// <remarks>
        /// Future drift is maximal allowed block's timestamp difference over adjusted time.
        /// If this difference is greater block won't be accepted.
        /// </remarks>
        /// <param name="time">UNIX timestamp.</param>
        /// <returns>Timestamp with maximum future drift applied.</returns>
        private long FutureDrift(long time)
        {
            return this.IsDriftReduced(time) ? time + 15 : time + 128 * 60 * 60;
        }

        /// <summary>
        /// Checks if block signature is valid.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <returns><c>true</c> if the signature is valid, <c>false</c> otherwise.</returns>
        private bool CheckBlockSignature(Block block)
        {
            this.logger.LogTrace("()");

            if (BlockStake.IsProofOfWork(block))
            {
                bool res = block.BlockSignatur.IsEmpty();
                this.logger.LogTrace("(-)[POW]:{0}", res);
                return res;
            }

            if (block.BlockSignatur.IsEmpty())
            {
                this.logger.LogTrace("(-)[EMPTY]:false");
                return false;
            }

            TxOut txout = block.Transactions[1].Outputs[1];

            if (PayToPubkeyTemplate.Instance.CheckScriptPubKey(txout.ScriptPubKey))
            {
                PubKey pubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(txout.ScriptPubKey);
                bool res = pubKey.Verify(block.GetHash(), new ECDSASignature(block.BlockSignatur.Signature));
                this.logger.LogTrace("(-)[P2PK]:{0}", res);
                return res;
            }

            // Block signing key also can be encoded in the nonspendable output.
            // This allows to not pollute UTXO set with useless outputs e.g. in case of multisig staking.

            List<Op> ops = txout.ScriptPubKey.ToOps().ToList();
            if (!ops.Any()) // script.GetOp(pc, opcode, vchPushValue))
            {
                this.logger.LogTrace("(-)[NO_OPS]:false");
                return false;
            }

            if (ops.ElementAt(0).Code != OpcodeType.OP_RETURN) // OP_RETURN)
            {
                this.logger.LogTrace("(-)[NO_OP_RETURN]:false");
                return false;
            }

            if (ops.Count < 2) // script.GetOp(pc, opcode, vchPushValue)
            {
                this.logger.LogTrace("(-)[NO_SECOND_OP]:false");
                return false;
            }

            byte[] data = ops.ElementAt(1).PushData;
            if (!ScriptEvaluationContext.IsCompressedOrUncompressedPubKey(data))
            {
                this.logger.LogTrace("(-)[NO_PUSH_DATA]:false");
                return false;
            }

            bool verifyRes = new PubKey(data).Verify(block.GetHash(this.ConsensusParams.NetworkOptions), new ECDSASignature(block.BlockSignatur.Signature));
            this.logger.LogTrace("(-):{0}", verifyRes);
            return verifyRes;
        }

        /// <inheritdoc />
        public override void CheckBlockHeader(RuleContext context)
        {
            this.logger.LogTrace("()");
            context.SetStake();

            if (context.Stake.BlockStake.IsProofOfWork())
            {
                if (context.CheckPow && !context.BlockValidationContext.Block.Header.CheckProofOfWork(context.Consensus))
                {
                    this.logger.LogTrace("(-)[HIGH_HASH]");
                    ConsensusErrors.HighHash.Throw();
                }
            }

            context.NextWorkRequired = this.StakeValidator.GetNextTargetRequired(this.stakeChain, context.BlockValidationContext.ChainedBlock.Previous, context.Consensus,
                context.Stake.BlockStake.IsProofOfStake());

            this.logger.LogTrace("(-)[OK]");
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

            ChainedBlock chainedBlock = context.BlockValidationContext.ChainedBlock;
            Block block = context.BlockValidationContext.Block;
            BlockStake blockStake = context.Stake.BlockStake;

            // Verify hash target and signature of coinstake tx.
            if (BlockStake.IsProofOfStake(block))
            {
                ChainedBlock prevChainedBlock = chainedBlock.Previous;

                BlockStake prevBlockStake = this.stakeChain.Get(prevChainedBlock.HashBlock);
                if (prevBlockStake == null)
                    ConsensusErrors.PrevStakeNull.Throw();

                // Only do proof of stake validation for blocks that are after the assumevalid block or after the last checkpoint.
                if (!context.SkipValidation)
                {
                    this.StakeValidator.CheckProofOfStake(context.Stake, prevChainedBlock, prevBlockStake, block.Transactions[1], chainedBlock.Header.Bits.ToCompact());
                }
                else this.logger.LogTrace("POS validation skipped for block at height {0}.", chainedBlock.Height);
            }

            // PoW is checked in CheckBlock().
            if (BlockStake.IsProofOfWork(block))
                context.Stake.HashProofOfStake = chainedBlock.Header.GetPoWHash();

            // Compute stake entropy bit for stake modifier.
            if (!blockStake.SetStakeEntropyBit(blockStake.GetStakeEntropyBit()))
            {
                this.logger.LogTrace("(-)[STAKE_ENTROPY_BIT_FAIL]");
                ConsensusErrors.SetStakeEntropyBitFailed.Throw();
            }

            // Record proof hash value.
            blockStake.HashProof = context.Stake.HashProofOfStake;

            int lastCheckpointHeight = this.Checkpoints.GetLastCheckpointHeight();
            if (chainedBlock.Height > lastCheckpointHeight)
            {
                // Compute stake modifier.
                ChainedBlock prevChainedBlock = chainedBlock.Previous;
                BlockStake blockStakePrev = prevChainedBlock == null ? null : this.stakeChain.Get(prevChainedBlock.HashBlock);
                blockStake.StakeModifierV2 = this.StakeValidator.ComputeStakeModifierV2(prevChainedBlock, blockStakePrev, blockStake.IsProofOfWork() ? chainedBlock.HashBlock : blockStake.PrevoutStake.Hash);
            }
            else if (chainedBlock.Height == lastCheckpointHeight)
            {
                // Copy checkpointed stake modifier.
                CheckpointInfo checkpoint = this.Checkpoints.GetCheckpoint(lastCheckpointHeight);
                blockStake.StakeModifierV2 = checkpoint.StakeModifierV2;
                this.logger.LogTrace("Last checkpoint stake modifier V2 loaded: '{0}'.", blockStake.StakeModifierV2);
            }
            else this.logger.LogTrace("POS stake modifier computation skipped for block at height {0} because it is not above last checkpoint block height {1}.", chainedBlock.Height, lastCheckpointHeight);

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
