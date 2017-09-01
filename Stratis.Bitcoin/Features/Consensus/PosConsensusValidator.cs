using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class PosConsensusValidator : PowConsensusValidator
    {
        // To decrease granularity of timestamp.
        // Supposed to be 2^n-1.
        public const uint StakeTimestampMask = 15;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly StakeValidator stakeValidator;
        public StakeValidator StakeValidator { get { return this.stakeValidator; } }

        private readonly StakeChain stakeChain;
        private readonly ConcurrentChain chain;
        private readonly CoinView coinView;
        private readonly PosConsensusOptions consensusOptions;

        public PosConsensusValidator(StakeValidator stakeValidator, Network network, StakeChain stakeChain, ConcurrentChain chain, CoinView coinView, ILoggerFactory loggerFactory)
            : base(network, loggerFactory)
        {
            Guard.NotNull(network.Consensus.Option<PosConsensusOptions>(), nameof(network.Consensus.Options));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.stakeValidator = stakeValidator;
            this.stakeChain = stakeChain;
            this.chain = chain;
            this.coinView = coinView;
            this.consensusOptions = network.Consensus.Option<PosConsensusOptions>();
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
            if (block.Header.Time > FutureDriftV2(DateTime.UtcNow.Ticks))
                ConsensusErrors.BlockTimestampTooFar.Throw();

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

        // TODO: https://github.com/stratisproject/StratisBitcoinFullNode/issues/383
        private static long FutureDriftV2(long nTime)
        {
            return nTime + 128 * 60 * 60;
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

            // Verify hash target and signature of coinstake tx.
            if (BlockStake.IsProofOfStake(block))
            {
                ChainedBlock pindexPrev = pindex.Previous;

                BlockStake prevBlockStake = this.stakeChain.Get(pindexPrev.HashBlock);
                if (prevBlockStake == null)
                    ConsensusErrors.PrevStakeNull.Throw();

                this.stakeValidator.CheckProofOfStake(context, pindexPrev, prevBlockStake, block.Transactions[1], pindex.Header.Bits.ToCompact());
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

            // Compute stake modifier.
            this.stakeValidator.ComputeStakeModifier(this.chain, pindex, blockStake);

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
