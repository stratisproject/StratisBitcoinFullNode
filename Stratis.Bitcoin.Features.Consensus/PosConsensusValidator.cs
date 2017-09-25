using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// TODO
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
    /// <code>hash(nStakeModifier + txPrev.block.nTime + txPrev.nTime + txPrev.vout.hash + txPrev.vout.n + nTime) < bnTarget * nWeight</code>
    /// <para>
    /// where 'txPrev' is the coinstake's kernel transaction, 'txPrev.vout' is the kernel's output in that transaction, 
    /// 'txPrev.vout.hash' is the hash of that transaction; 'nTime' is coinstake's transaction time; 'bnTarget' is the target as 
    /// in 'nBits' block header; 'nWeight' is the value of the kernel's input. 
    /// </para>
    /// </item>
    /// <item>Block's height MUST NOT be more than 500 blocks back - i.e. reorganizations longer than 500 are not allowed.</item>
    /// <item>Coinbase 'scriptSig' starts with serialized block height value. This means that coinbase transaction commits to the height of the block it appears in.</item>
    /// </list>
    /// </remarks>
    public class PosConsensusValidator : PowConsensusValidator
    {
        // To decrease granularity of timestamp.
        // Supposed to be 2^n-1.
        public const uint StakeTimestampMask = 0x0000000F;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        private readonly StakeValidator stakeValidator;
        public StakeValidator StakeValidator { get { return this.stakeValidator; } }

        private readonly StakeChain stakeChain;
        private readonly ConcurrentChain chain;
        private readonly CoinView coinView;
        private readonly PosConsensusOptions consensusOptions;

        public PosConsensusValidator(StakeValidator stakeValidator, ICheckpoints checkpoints, Network network, StakeChain stakeChain, ConcurrentChain chain, CoinView coinView, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
            : base(network, checkpoints, loggerFactory)
        {
            Guard.NotNull(network.Consensus.Option<PosConsensusOptions>(), nameof(network.Consensus.Options));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.stakeValidator = stakeValidator;
            this.stakeChain = stakeChain;
            this.chain = chain;
            this.coinView = coinView;
            this.consensusOptions = network.Consensus.Option<PosConsensusOptions>();
            this.dateTimeProvider = dateTimeProvider;
        }

        public override void CheckBlockReward(ContextInformation context, Money nFees, ChainedBlock chainedBlock, Block block)
        {
            this.logger.LogTrace("({0}:{1},{2}:'{3}/{4}')", nameof(nFees), nFees, nameof(chainedBlock), chainedBlock.HashBlock, chainedBlock.Height);

            if (BlockStake.IsProofOfStake(block))
            {
                // proof of stake invalidates previous inputs 
                // and spends the inputs to new outputs with the 
                // additional stake reward, next calculate the  
                // reward does not exceed the consensus rules  

                Money stakeReward = block.Transactions[1].TotalOut - context.Stake.TotalCoinStakeValueIn;
                Money calcStakeReward = nFees + this.GetProofOfStakeReward(chainedBlock.Height);

                this.logger.LogTrace("Block stake reward is {0}, calculated reward is {1}.", stakeReward, calcStakeReward);
                if (stakeReward > calcStakeReward)
                {
                    this.logger.LogTrace("(-)[BAD_COINSTAKE_AMOUNT]");
                    ConsensusErrors.BadCoinstakeAmount.Throw();
                }
            }
            else
            {
                Money blockReward = nFees + this.GetProofOfWorkReward(chainedBlock.Height);
                this.logger.LogTrace("Block reward is {0}, calculated reward is {1}.", block.Transactions[0].TotalOut, blockReward);
                if (block.Transactions[0].TotalOut > blockReward)
                {
                    this.logger.LogTrace("(-)[BAD_COINBASE_AMOUNT]");
                    ConsensusErrors.BadCoinbaseAmount.Throw();
                }
            }

            this.logger.LogTrace("(-)");
        }

        public override void ExecuteBlock(ContextInformation context, TaskScheduler taskScheduler)
        {
            this.logger.LogTrace("()");

            // Compute and store the stake proofs.
            this.CheckAndComputeStake(context);

            base.ExecuteBlock(context, taskScheduler);

            // TODO: A temporary fix till this methods is fixed in NStratis.
            (this.stakeChain as StakeChainStore).Set(context.BlockResult.ChainedBlock, context.Stake.BlockStake);

            this.logger.LogTrace("(-)");
        }

        public override void CheckBlock(ContextInformation context)
        {
            this.logger.LogTrace("()");

            base.CheckBlock(context);

            Block block = context.BlockResult.Block;

            // Check timestamp.
            if (block.Header.Time > FutureDriftV2(this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp()))
            {
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
            if (!CheckBlockSignature(block))
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

            this.logger.LogTrace("(-)");
        }

        protected override void UpdateCoinView(ContextInformation context, Transaction tx)
        {
            this.logger.LogTrace("()");

            UnspentOutputSet view = context.Set;

            if (tx.IsCoinStake)
                context.Stake.TotalCoinStakeValueIn = view.GetValueIn(tx);

            base.UpdateCoinView(context, tx);

            this.logger.LogTrace("(-)");
        }

        protected override void CheckMaturity(UnspentOutputs coins, int nSpendHeight)
        {
            this.logger.LogTrace("({0}:'{1}/{2}',{3}:{4})", nameof(coins), coins.TransactionId, coins.Height, nameof(nSpendHeight), nSpendHeight);

            base.CheckMaturity(coins, nSpendHeight);

            if (coins.IsCoinstake)
            {
                if ((nSpendHeight - coins.Height) < this.consensusOptions.COINBASE_MATURITY)
                {
                    this.logger.LogTrace("Coinstake transaction height {0} spent at height {1}, but maturity is set to {2}.", coins.Height, nSpendHeight, this.consensusOptions.COINBASE_MATURITY);
                    this.logger.LogTrace("(-)[COINSTAKE_PREMATURE_SPENDING]");
                    ConsensusErrors.BadTransactionPrematureCoinstakeSpending.Throw();
                }
            }

            this.logger.LogTrace("(-)");
        }

        public override void ContextualCheckBlock(ContextInformation context)
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

            this.logger.LogTrace("(-)");
        }

        public override void ContextualCheckBlockHeader(ContextInformation context)
        {
            this.logger.LogTrace("()");
            base.ContextualCheckBlockHeader(context);

            ChainedBlock chainedBlock = context.BlockResult.ChainedBlock;
            this.logger.LogTrace("Height of block is {0}, block timestamp is {1}, previous block timestamp is {2}.", chainedBlock.Height, chainedBlock.Header.Time, chainedBlock.Previous.Header.Time);

            if (!StakeValidator.IsProtocolV3((int)chainedBlock.Header.Time))
            {
                if (chainedBlock.Header.Version > BlockHeader.CURRENT_VERSION)
                {
                    ConsensusErrors.BadVersion.Throw();
                    this.logger.LogTrace("(-)[BAD_VERSION_NO_V3]");
                }
            }

            if (StakeValidator.IsProtocolV2(chainedBlock.Height) && (chainedBlock.Header.Version < 7))
            {
                this.logger.LogTrace("(-)[BAD_VERSION_V2_LT_7]");
                ConsensusErrors.BadVersion.Throw();
            }

            if (!StakeValidator.IsProtocolV2(chainedBlock.Height) && (chainedBlock.Header.Version > 6))
            {
                this.logger.LogTrace("(-)[BAD_VERSION_V1_GT_6]");
                ConsensusErrors.BadVersion.Throw();
            }

            if (context.Stake.BlockStake.IsProofOfWork() && (chainedBlock.Height > this.ConsensusParams.LastPOWBlock))
            {
                this.logger.LogTrace("(-)[POW_TOO_HIGH]");
                ConsensusErrors.ProofOfWorkTooHeigh.Throw();
            }

            // Check coinbase timestamp.
            if (chainedBlock.Header.Time > FutureDrift(context.BlockResult.Block.Transactions[0].Time, chainedBlock.Height))
            {
                this.logger.LogTrace("(-)[TIME_TOO_NEW]");
                ConsensusErrors.TimeTooNew.Throw();
            }

            // Check coinstake timestamp.
            if (context.Stake.BlockStake.IsProofOfStake()
                && !PosConsensusValidator.CheckCoinStakeTimestamp(chainedBlock.Height, chainedBlock.Header.Time, context.BlockResult.Block.Transactions[1].Time))
            {
                this.logger.LogTrace("(-)[BAD_TIME]");
                ConsensusErrors.StakeTimeViolation.Throw();
            }

            // Check timestamp against prev.
            if ((chainedBlock.Header.Time <= StakeValidator.GetPastTimeLimit(chainedBlock.Previous))
                || (FutureDrift(chainedBlock.Header.Time, chainedBlock.Height) < chainedBlock.Previous.Header.Time))
            {
                this.logger.LogTrace("(-)[TIME_TOO_EARLY]");
                ConsensusErrors.BlockTimestampTooEarly.Throw();
            }

            this.logger.LogTrace("(-)");
        }

        // Check whether the coinstake timestamp meets protocol.
        public static bool CheckCoinStakeTimestamp(int nHeight, long nTimeBlock, long nTimeTx)
        {
            if (StakeValidator.IsProtocolV2(nHeight))
                return (nTimeBlock == nTimeTx) && ((nTimeTx & StakeTimestampMask) == 0);
            else
                return (nTimeBlock == nTimeTx);
        }

        private static long FutureDriftV1(long nTime)
        {
            return nTime + 10 * 60;
        }

        private static bool IsDriftReduced(long nTime)
        {
            return nTime > 1479513600;
        }

        private static long FutureDriftV2(long nTime)
        {
            return IsDriftReduced(nTime) ? nTime + 15 : nTime + 128 * 60 * 60;
        }

        private static long FutureDrift(long nTime, int nHeight)
        {
            return StakeValidator.IsProtocolV2(nHeight) ? FutureDriftV2(nTime) : FutureDriftV1(nTime);
        }

        public bool CheckBlockSignature(Block block)
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

            if (StakeValidator.IsProtocolV3((int)block.Header.Time))
            {
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

                bool res = new PubKey(data).Verify(block.GetHash(), new ECDSASignature(block.BlockSignatur.Signature));
                this.logger.LogTrace("(-):{0}", res);
                return res;
            }

            this.logger.LogTrace("(-)[VERSION]:false");
            return false;
        }

        public override void CheckBlockHeader(ContextInformation context)
        {
            this.logger.LogTrace("()");
            context.SetStake();

            if (context.Stake.BlockStake.IsProofOfWork())
            {
                if (context.CheckPow && !context.BlockResult.Block.Header.CheckProofOfWork())
                {
                    this.logger.LogTrace("(-)[HIGH_HASH]");
                    ConsensusErrors.HighHash.Throw();
                }
            }

            context.NextWorkRequired = StakeValidator.GetNextTargetRequired(this.stakeChain, context.BlockResult.ChainedBlock.Previous, context.Consensus,
                context.Stake.BlockStake.IsProofOfStake());

            this.logger.LogTrace("(-)");
        }

        public void CheckAndComputeStake(ContextInformation context)
        {
            this.logger.LogTrace("()");

            ChainedBlock pindex = context.BlockResult.ChainedBlock;
            Block block = context.BlockResult.Block;
            BlockStake blockStake = context.Stake.BlockStake;

            int lastCheckpointHeight = this.checkpoints.GetLastCheckpointHeight();

            // Verify hash target and signature of coinstake tx.
            if (BlockStake.IsProofOfStake(block))
            {
                ChainedBlock pindexPrev = pindex.Previous;

                BlockStake prevBlockStake = this.stakeChain.Get(pindexPrev.HashBlock);
                if (prevBlockStake == null)
                    ConsensusErrors.PrevStakeNull.Throw();

                // Only do proof of stake validation for blocks after last checkpoint.
                if (pindex.Height > lastCheckpointHeight)
                {
                    this.stakeValidator.CheckProofOfStake(context, pindexPrev, prevBlockStake, block.Transactions[1], pindex.Header.Bits.ToCompact());
                }
                else this.logger.LogTrace("POS validation skipped for block at height {0} because it is below last checkpoint block height {1}.", pindex.Height, lastCheckpointHeight);
            }

            // PoW is checked in CheckBlock().
            if (BlockStake.IsProofOfWork(block))
                context.Stake.HashProofOfStake = pindex.Header.GetPoWHash();

            // TODO: Is this the same as chain work?
            // Compute chain trust score.
            //pindexNew.nChainTrust = (pindexNew->pprev ? pindexNew->pprev->nChainTrust : 0) + pindexNew->GetBlockTrust();

            // Compute stake entropy bit for stake modifier.
            if (!blockStake.SetStakeEntropyBit(blockStake.GetStakeEntropyBit()))
            {
                this.logger.LogTrace("(-)[STAKE_ENTROPY_BIT_FAIL]");
                ConsensusErrors.SetStakeEntropyBitFailed.Throw();
            }

            // Record proof hash value.
            blockStake.HashProof = context.Stake.HashProofOfStake;

            if (pindex.Height > lastCheckpointHeight)
            {
                // Compute stake modifier.
                this.stakeValidator.ComputeStakeModifier(this.chain, pindex, blockStake);
            }
            else if (pindex.Height == lastCheckpointHeight)
            {
                // Copy checkpointed stake modifier.
                CheckpointInfo checkpoint = this.checkpoints.GetCheckpoint(lastCheckpointHeight);
                blockStake.StakeModifierV2 = checkpoint.StakeModifierV2;
                this.logger.LogTrace("Last checkpoint stake modifier V2 loaded: '{0}'.", blockStake.StakeModifierV2);
            }
            else this.logger.LogTrace("POS stake modifier computation skipped for block at height {0} because it is below last checkpoint block height {1}.", pindex.Height, lastCheckpointHeight);

            this.logger.LogTrace("(-)");
        }

        public override Money GetProofOfWorkReward(int height)
        {
            if (this.IsPremine(height))
                return this.consensusOptions.PremineReward;

            return this.ConsensusOptions.ProofOfWorkReward;
        }

        // Miner's coin stake reward.
        public Money GetProofOfStakeReward(int height)
        {
            if (this.IsPremine(height))
                return this.consensusOptions.PremineReward;

            return this.consensusOptions.ProofOfStakeReward;
        }

        private bool IsPremine(int height)
        {
            return (this.consensusOptions.PremineHeight > 0) &&
                   (this.consensusOptions.PremineReward > 0) &&
                   (height == this.consensusOptions.PremineHeight);
        }
    }
}
