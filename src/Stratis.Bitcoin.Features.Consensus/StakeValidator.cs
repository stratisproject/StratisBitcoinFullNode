using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// Provides functionality for verifying validity of PoS block.
    /// </summary>
    /// See <see cref="Stratis.Bitcoin.Features.Miner.PosMinting"/> for more information about PoS solutions.
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
    public class StakeValidator : IStakeValidator
    {
        /// <summary>When checking the POS block signature this determines the maximum push data (public key) size following the OP_RETURN in the nonspendable output.</summary>
        private const int MaxPushDataSize = 40;

        /// <summary>Expected (or target) block time in seconds.</summary>
        public const uint TargetSpacingSeconds = 64;

        /// <summary>Time interval in minutes that is used in the retarget calculation.</summary>
        private const uint RetargetIntervalMinutes = 16;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Database of stake related data for the current blockchain.</summary>
        private readonly IStakeChain stakeChain;

        /// <summary>Thread safe access to the best chain of block headers (that the node is aware of) from genesis.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Consensus' view of UTXO set.</summary>
        private readonly ICoinView coinView;

        /// <inheritdoc cref="Network"/>
        private readonly Network network;

        /// <inheritdoc />
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="stakeChain">Database of stake related data for the current blockchain.</param>
        /// <param name="chain">Chain of headers.</param>
        /// <param name="coinView">Used for getting UTXOs.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public StakeValidator(Network network, IStakeChain stakeChain, ConcurrentChain chain, ICoinView coinView, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.stakeChain = stakeChain;
            this.chain = chain;
            this.coinView = coinView;
            this.network = network;
        }

        /// <inheritdoc/>
        public ChainedHeader GetLastPowPosChainedBlock(IStakeChain stakeChain, ChainedHeader startChainedHeader, bool proofOfStake)
        {
            Guard.NotNull(stakeChain, nameof(stakeChain));
            Guard.Assert(startChainedHeader != null);

            BlockStake blockStake = stakeChain.Get(startChainedHeader.HashBlock);

            while ((startChainedHeader.Previous != null) && (blockStake.IsProofOfStake() != proofOfStake))
            {
                startChainedHeader = startChainedHeader.Previous;
                blockStake = stakeChain.Get(startChainedHeader.HashBlock);
            }

            return startChainedHeader;
        }

        /// <inheritdoc/>
        public Target CalculateRetarget(uint firstBlockTime, Target firstBlockTarget, uint secondBlockTime, BigInteger targetLimit)
        {
            uint targetSpacing = TargetSpacingSeconds;
            uint actualSpacing = firstBlockTime > secondBlockTime ? firstBlockTime - secondBlockTime : targetSpacing;

            if (actualSpacing > targetSpacing * 10)
                actualSpacing = targetSpacing * 10;

            uint targetTimespan = RetargetIntervalMinutes * 60;
            uint interval = targetTimespan / targetSpacing;

            BigInteger target = firstBlockTarget.ToBigInteger();

            long multiplyBy = (interval - 1) * targetSpacing + actualSpacing + actualSpacing;
            target = target.Multiply(BigInteger.ValueOf(multiplyBy));

            long divideBy = (interval + 1) * targetSpacing;
            target = target.Divide(BigInteger.ValueOf(divideBy));

            this.logger.LogTrace("The next target difficulty will be {0} times higher (easier to satisfy) than the previous target.", (double)multiplyBy / (double)divideBy);

            if ((target.CompareTo(BigInteger.Zero) <= 0) || (target.CompareTo(targetLimit) >= 1))
                target = targetLimit;

            var finalTarget = new Target(target);

            return finalTarget;
        }

        /// <inheritdoc/>
        public Target GetNextTargetRequired(IStakeChain stakeChain, ChainedHeader chainedHeader, IConsensus consensus, bool proofOfStake)
        {
            Guard.NotNull(stakeChain, nameof(stakeChain));

            // Genesis block.
            if (chainedHeader == null)
            {
                this.logger.LogTrace("(-)[GENESIS]:'{0}'", consensus.PowLimit);
                return consensus.PowLimit;
            }

            // Find the last two blocks that correspond to the mining algo
            // (i.e if this is a POS block we need to find the last two POS blocks).
            BigInteger targetLimit = proofOfStake
                ? consensus.ProofOfStakeLimitV2
                : consensus.PowLimit.ToBigInteger();

            // First block.
            ChainedHeader lastPowPosBlock = this.GetLastPowPosChainedBlock(stakeChain, chainedHeader, proofOfStake);
            if (lastPowPosBlock.Previous == null)
            {
                var res = new Target(targetLimit);
                this.logger.LogTrace("(-)[FIRST_BLOCK]:'{0}'", res);
                return res;
            }

            // Second block.
            ChainedHeader prevLastPowPosBlock = this.GetLastPowPosChainedBlock(stakeChain, lastPowPosBlock.Previous, proofOfStake);
            if (prevLastPowPosBlock.Previous == null)
            {
                var res = new Target(targetLimit);
                this.logger.LogTrace("(-)[SECOND_BLOCK]:'{0}'", res);
                return res;
            }

            // This is used in tests to allow quickly mining blocks.
            if (consensus.PowNoRetargeting)
            {
                this.logger.LogTrace("(-)[NO_POW_RETARGET]:'{0}'", lastPowPosBlock.Header.Bits);
                return lastPowPosBlock.Header.Bits;
            }

            Target finalTarget = this.CalculateRetarget(lastPowPosBlock.Header.Time, lastPowPosBlock.Header.Bits, prevLastPowPosBlock.Header.Time, targetLimit);

            return finalTarget;
        }

        /// <inheritdoc/>
        public void CheckProofOfStake(PosRuleContext context, ChainedHeader prevChainedHeader, BlockStake prevBlockStake, Transaction transaction, uint headerBits)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(prevChainedHeader, nameof(prevChainedHeader));
            Guard.NotNull(prevBlockStake, nameof(prevBlockStake));
            Guard.NotNull(transaction, nameof(transaction));

            if (!transaction.IsCoinStake)
            {
                this.logger.LogTrace("(-)[NO_COINSTAKE]");
                ConsensusErrors.NonCoinstake.Throw();
            }

            TxIn txIn = transaction.Inputs[0];

            UnspentOutputs prevUtxo = context.UnspentOutputSet.AccessCoins(txIn.PrevOut.Hash);
            if (prevUtxo == null)
            {
                this.logger.LogTrace("(-)[PREV_UTXO_IS_NULL]");
                ConsensusErrors.ReadTxPrevFailed.Throw();
            }

            // Verify signature.
            if (!this.VerifySignature(prevUtxo, transaction, 0, ScriptVerify.None))
            {
                this.logger.LogTrace("(-)[BAD_SIGNATURE]");
                ConsensusErrors.CoinstakeVerifySignatureFailed.Throw();
            }

            // Min age requirement.
            if (this.IsConfirmedInNPrevBlocks(prevUtxo, prevChainedHeader, this.GetTargetDepthRequired(prevChainedHeader)))
            {
                this.logger.LogTrace("(-)[BAD_STAKE_DEPTH]");
                ConsensusErrors.InvalidStakeDepth.Throw();
            }

            if (!this.CheckStakeKernelHash(context, headerBits, prevBlockStake.StakeModifierV2, prevUtxo, txIn.PrevOut, transaction.Time))
            {
                this.logger.LogTrace("(-)[INVALID_STAKE_HASH_TARGET]");
                ConsensusErrors.StakeHashInvalidTarget.Throw();
            }
        }

        /// <inheritdoc/>
        public uint256 ComputeStakeModifierV2(ChainedHeader prevChainedHeader, uint256 prevStakeModifier, uint256 kernel)
        {
            Guard.NotNull(prevStakeModifier, nameof(prevStakeModifier));
            if (prevChainedHeader == null)
                return 0; // Genesis block's modifier is 0.

            uint256 stakeModifier;
            using (var ms = new MemoryStream())
            {
                var serializer = new BitcoinStream(ms, true);
                serializer.ReadWrite(kernel);
                serializer.ReadWrite(prevStakeModifier);
                stakeModifier = Hashes.Hash256(ms.ToArray());
            }

            return stakeModifier;
        }

        /// <inheritdoc/>
        public bool CheckKernel(PosRuleContext context, ChainedHeader prevChainedHeader, uint headerBits, long transactionTime, OutPoint prevout)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(prevout, nameof(prevout));
            Guard.NotNull(prevChainedHeader, nameof(prevChainedHeader));

            FetchCoinsResponse coins = this.coinView.FetchCoinsAsync(new[] { prevout.Hash }).GetAwaiter().GetResult();
            if ((coins == null) || (coins.UnspentOutputs.Length != 1))
            {
                this.logger.LogTrace("(-)[READ_PREV_TX_FAILED]");
                ConsensusErrors.ReadTxPrevFailed.Throw();
            }

            ChainedHeader prevBlock = this.chain.GetBlock(coins.BlockHash);
            if (prevBlock == null)
            {
                this.logger.LogTrace("(-)[REORG]");
                ConsensusErrors.ReadTxPrevFailed.Throw();
            }

            UnspentOutputs prevUtxo = coins.UnspentOutputs[0];
            if (prevUtxo == null)
            {
                this.logger.LogTrace("(-)[PREV_UTXO_IS_NULL]");
                ConsensusErrors.ReadTxPrevFailed.Throw();
            }

            if (this.IsConfirmedInNPrevBlocks(prevUtxo, prevChainedHeader, this.GetTargetDepthRequired(prevChainedHeader)))
            {
                this.logger.LogTrace("(-)[LOW_COIN_AGE]");
                ConsensusErrors.InvalidStakeDepth.Throw();
            }

            BlockStake prevBlockStake = this.stakeChain.Get(prevChainedHeader.HashBlock);
            if (prevBlockStake == null)
            {
                this.logger.LogTrace("(-)[BAD_STAKE_BLOCK]");
                ConsensusErrors.BadStakeBlock.Throw();
            }

            return this.CheckStakeKernelHash(context, headerBits, prevBlockStake.StakeModifierV2, prevUtxo, prevout, (uint)transactionTime);
        }

        /// <inheritdoc/>
        public bool CheckStakeKernelHash(PosRuleContext context, uint headerBits, uint256 prevStakeModifier, UnspentOutputs stakingCoins, OutPoint prevout, uint transactionTime)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(prevout, nameof(prevout));
            Guard.NotNull(stakingCoins, nameof(stakingCoins));

            if (transactionTime < stakingCoins.Time)
            {
                this.logger.LogTrace("Coinstake transaction timestamp {0} is lower than it's own UTXO timestamp {1}.", transactionTime, stakingCoins.Time);
                this.logger.LogTrace("(-)[BAD_STAKE_TIME]");
                ConsensusErrors.StakeTimeViolation.Throw();
            }

            // Base target.
            BigInteger target = new Target(headerBits).ToBigInteger();

            // TODO: Investigate:
            // The POS protocol should probably put a limit on the max amount that can be staked
            // not a hard limit but a limit that allow any amount to be staked with a max weight value.
            // the max weight should not exceed the max uint256 array size (array size = 32).

            // Weighted target.
            long valueIn = stakingCoins.Outputs[prevout.N].Value.Satoshi;
            BigInteger weight = BigInteger.ValueOf(valueIn);
            BigInteger weightedTarget = target.Multiply(weight);

            context.TargetProofOfStake = this.ToUInt256(weightedTarget);
            this.logger.LogTrace("POS target is '{0}', weighted target for {1} coins is '{2}'.", this.ToUInt256(target), valueIn, context.TargetProofOfStake);

            // Calculate hash.
            using (var ms = new MemoryStream())
            {
                var serializer = new BitcoinStream(ms, true);
                serializer.ReadWrite(prevStakeModifier);
                serializer.ReadWrite(stakingCoins.Time);
                serializer.ReadWrite(prevout.Hash);
                serializer.ReadWrite(prevout.N);
                serializer.ReadWrite(transactionTime);

                context.HashProofOfStake = Hashes.Hash256(ms.ToArray());
            }

            this.logger.LogTrace("Stake modifier V2 is '{0}', hash POS is '{1}'.", prevStakeModifier, context.HashProofOfStake);

            // Now check if proof-of-stake hash meets target protocol.
            var hashProofOfStakeTarget = new BigInteger(1, context.HashProofOfStake.ToBytes(false));
            if (hashProofOfStakeTarget.CompareTo(weightedTarget) > 0)
            {
                this.logger.LogTrace("(-)[TARGET_MISSED]");
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public bool VerifySignature(UnspentOutputs coin, Transaction txTo, int txToInN, ScriptVerify flagScriptVerify)
        {
            Guard.NotNull(coin, nameof(coin));
            Guard.NotNull(txTo, nameof(txTo));

            if (txToInN < 0 || txToInN >= txTo.Inputs.Count)
                return false;

            TxIn input = txTo.Inputs[txToInN];

            if (input.PrevOut.N >= coin.Outputs.Length)
            {
                this.logger.LogTrace("(-)[OUTPUT_INCORRECT_LENGTH]");
                return false;
            }

            if (input.PrevOut.Hash != coin.TransactionId)
            {
                this.logger.LogTrace("(-)[INCORRECT_TX]");
                return false;
            }

            TxOut output = coin.Outputs[input.PrevOut.N];

            if (output == null)
            {
                this.logger.LogTrace("(-)[OUTPUT_NOT_FOUND]");
                return false;
            }

            var txData = new PrecomputedTransactionData(txTo);
            var checker = new TransactionChecker(txTo, txToInN, output.Value, txData);
            var ctx = new ScriptEvaluationContext(this.chain.Network) { ScriptVerify = flagScriptVerify };

            bool res = ctx.VerifyScript(input.ScriptSig, output.ScriptPubKey, checker);
            return res;
        }

        /// <inheritdoc />
        public bool IsConfirmedInNPrevBlocks(UnspentOutputs coins, ChainedHeader referenceChainedHeader, long targetDepth)
        {
            Guard.NotNull(coins, nameof(coins));
            Guard.NotNull(referenceChainedHeader, nameof(referenceChainedHeader));

            int actualDepth = referenceChainedHeader.Height - (int)coins.Height;
            bool res = actualDepth < targetDepth;

            return res;
        }

        /// <inheritdoc />
        public long GetTargetDepthRequired(ChainedHeader prevChainedHeader)
        {
            Guard.NotNull(prevChainedHeader, nameof(ChainedHeader));

            return (this.network.Consensus.Options as PosConsensusOptions).GetStakeMinConfirmations(prevChainedHeader.Height + 1, this.network) - 1;
        }

        /// <summary>
        /// Converts <see cref="BigInteger" /> to <see cref="uint256" />.
        /// </summary>
        /// <param name="input"><see cref="BigInteger"/> input value.</param>
        /// <returns><see cref="uint256"/> version of <paramref name="input"/>.</returns>
        private uint256 ToUInt256(BigInteger input)
        {
            byte[] array = input.ToByteArray();

            int missingZero = 32 - array.Length;

            if (missingZero < 0)
                return new uint256(array.Skip(Math.Abs(missingZero)).ToArray(), false);

            if (missingZero > 0)
                return new uint256(new byte[missingZero].Concat(array).ToArray(), false);

            return new uint256(array, false);
        }

        /// <inheritdoc />
        public bool CheckStakeSignature(BlockSignature signature, uint256 blockHash, Transaction coinStake)
        {
            if (signature.IsEmpty())
            {
                this.logger.LogTrace("(-)[EMPTY]:false");
                return false;
            }

            TxOut txout = coinStake.Outputs[1];

            if (PayToPubkeyTemplate.Instance.CheckScriptPubKey(txout.ScriptPubKey))
            {
                PubKey pubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(txout.ScriptPubKey);
                bool res = pubKey.Verify(blockHash, new ECDSASignature(signature.Signature));
                this.logger.LogTrace("(-)[P2PK]:{0}", res);
                return res;
            }

            // Block signing key also can be encoded in the nonspendable output.
            // This allows to not pollute UTXO set with useless outputs e.g. in case of multisig staking.

            List<Op> ops = txout.ScriptPubKey.ToOps().ToList();
            if (!ops.Any())
            {
                this.logger.LogTrace("(-)[NO_OPS]:false");
                return false;
            }

            if (ops.ElementAt(0).Code != OpcodeType.OP_RETURN) // OP_RETURN)
            {
                this.logger.LogTrace("(-)[NO_OP_RETURN]:false");
                return false;
            }

            if (ops.Count != 2)
            {
                this.logger.LogTrace("(-)[INVALID_OP_COUNT]:false");
                return false;
            }

            byte[] data = ops.ElementAt(1).PushData;

            if (data.Length > MaxPushDataSize)
            {
                this.logger.LogTrace("(-)[PUSH_DATA_TOO_LARGE]:false");
                return false;
            }

            if (!ScriptEvaluationContext.IsCompressedOrUncompressedPubKey(data))
            {
                this.logger.LogTrace("(-)[NO_PUSH_DATA]:false");
                return false;
            }

            bool verifyRes = new PubKey(data).Verify(blockHash, new ECDSASignature(signature.Signature));
            return verifyRes;
        }
    }
}

