using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// Provides functionality for checking validity of PoS blocks.
    /// See <see cref="Stratis.Bitcoin.Features.Miner.PosMinting"/> for more information about PoS solutions.
    /// </summary>
    public class StakeValidator : IStakeValidator
    {
        /// <summary>Expected (or target) block time in seconds.</summary>
        public const int TargetSpacingSeconds = 64;

        /// <summary>Time interval in minutes that is used in the retarget calculation.</summary>
        private const int RetargetIntervalMinutes = 16;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Database of stake related data for the current blockchain.</summary>
        private readonly IStakeChain stakeChain;

        /// <summary>Thread safe access to the best chain of block headers (that the node is aware of) from genesis.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Consensus' view of UTXO set.</summary>
        private readonly CoinView coinView;

        /// <summary>Defines a set of options that are used by the consensus rules of Proof Of Stake (POS).</summary>
        private readonly PosConsensusOptions consensusOptions;

        /// <inheritdoc />
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="stakeChain">Database of stake related data for the current blockchain.</param>
        /// <param name="chain">Chain of headers.</param>
        /// <param name="coinView">Used for getting UTXOs.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public StakeValidator(Network network, IStakeChain stakeChain, ConcurrentChain chain, CoinView coinView, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.stakeChain = stakeChain;
            this.chain = chain;
            this.coinView = coinView;
            this.consensusOptions = network.Consensus.Option<PosConsensusOptions>();
        }

        /// <inheritdoc/>
        public ChainedBlock GetLastPowPosChainedBlock(IStakeChain stakeChain, ChainedBlock startChainedBlock, bool proofOfStake)
        {
            Guard.Assert(startChainedBlock != null);

            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(startChainedBlock), startChainedBlock, nameof(proofOfStake), proofOfStake);

            BlockStake blockStake = stakeChain.Get(startChainedBlock.HashBlock);

            while ((startChainedBlock.Previous != null) && (blockStake.IsProofOfStake() != proofOfStake))
            {
                startChainedBlock = startChainedBlock.Previous;
                blockStake = stakeChain.Get(startChainedBlock.HashBlock);
            }

            this.logger.LogTrace("(-)':{0}'", startChainedBlock);
            return startChainedBlock;
        }

        /// <inheritdoc/>
        public Target GetNextTargetRequired(IStakeChain stakeChain, ChainedBlock chainedBlock, NBitcoin.Consensus consensus, bool proofOfStake)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(chainedBlock), chainedBlock, nameof(proofOfStake), proofOfStake);

            // Genesis block.
            if (chainedBlock == null)
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
            ChainedBlock lastPowPosBlock = GetLastPowPosChainedBlock(stakeChain, chainedBlock, proofOfStake);
            if (lastPowPosBlock.Previous == null)
            {
                var res = new Target(targetLimit);
                this.logger.LogTrace("(-)[FIRST_BLOCK]:'{0}'", res);
                return res;
            }

            // Second block.
            ChainedBlock prevLastPowPosBlock = GetLastPowPosChainedBlock(stakeChain, lastPowPosBlock.Previous, proofOfStake);
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

            int targetSpacing = TargetSpacingSeconds;
            int actualSpacing = (int)(lastPowPosBlock.Header.Time - prevLastPowPosBlock.Header.Time);
            if (actualSpacing < 0)
                actualSpacing = targetSpacing;

            if (actualSpacing > targetSpacing * 10)
                actualSpacing = targetSpacing * 10;

            int targetTimespan = RetargetIntervalMinutes * 60;
            int interval = targetTimespan / targetSpacing;

            BigInteger target = lastPowPosBlock.Header.Bits.ToBigInteger();

            long multiplyBy = (interval - 1) * targetSpacing + actualSpacing + actualSpacing;
            target = target.Multiply(BigInteger.ValueOf(multiplyBy));

            long divideBy = (interval + 1) * targetSpacing;
            target = target.Divide(BigInteger.ValueOf(divideBy));

            this.logger.LogTrace("The next target difficulty will be {0} times higher (easier to satisfy) than the previous target.", (double)multiplyBy / (double)divideBy);

            if ((target.CompareTo(BigInteger.Zero) <= 0) || (target.CompareTo(targetLimit) >= 1))
                target = targetLimit;

            var finalTarget = new Target(target);
            this.logger.LogTrace("(-):'{0}'", finalTarget);
            return finalTarget;
        }

        /// <inheritdoc/>
        public void CheckProofOfStake(ContextStakeInformation context, ChainedBlock prevChainedBlock, BlockStake prevBlockStake, Transaction transaction, uint headerBits)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:'{4}',{5}:0x{6:X})", nameof(prevChainedBlock), prevChainedBlock.HashBlock, nameof(prevBlockStake), nameof(prevBlockStake.HashProof), prevBlockStake.HashProof, nameof(headerBits), headerBits);

            if (!transaction.IsCoinStake)
            {
                this.logger.LogTrace("(-)[NO_COINSTAKE]");
                ConsensusErrors.NonCoinstake.Throw();
            }

            TxIn txIn = transaction.Inputs[0];

            // First try finding the previous transaction in database.
            FetchCoinsResponse coins = this.coinView.FetchCoinsAsync(new[] { txIn.PrevOut.Hash }).GetAwaiter().GetResult();
            if ((coins == null) || (coins.UnspentOutputs.Length != 1))
                ConsensusErrors.ReadTxPrevFailed.Throw();

            UnspentOutputs prevUtxo = coins.UnspentOutputs[0];
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
            if (this.IsConfirmedInNPrevBlocks(prevUtxo, prevChainedBlock, this.consensusOptions.StakeMinConfirmations - 1))
            {
                this.logger.LogTrace("(-)[BAD_STAKE_DEPTH]");
                ConsensusErrors.InvalidStakeDepth.Throw();
            }

            this.CheckStakeKernelHash(context, headerBits, prevBlockStake, prevUtxo, txIn.PrevOut, transaction.Time);

            this.logger.LogTrace("(-)[OK]");
        }

        /// <inheritdoc/>
        public uint256 ComputeStakeModifierV2(ChainedBlock prevChainedBlock, BlockStake blockStakePrev, uint256 kernel)
        {
            if (prevChainedBlock == null)
                return 0; // Genesis block's modifier is 0.

            uint256 stakeModifier;
            using (var ms = new MemoryStream())
            {
                var serializer = new BitcoinStream(ms, true);
                serializer.ReadWrite(kernel);
                serializer.ReadWrite(blockStakePrev.StakeModifierV2);
                stakeModifier = Hashes.Hash256(ms.ToArray());
            }

            return stakeModifier;
        }

        /// <inheritdoc/>
        public void CheckKernel(ContextStakeInformation context, ChainedBlock prevChainedBlock, uint headerBits, long transactionTime, OutPoint prevout)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:0x{3:X},{4}:{5},{6}:'{7}.{8}')", nameof(prevChainedBlock), prevChainedBlock,
                nameof(headerBits), headerBits, nameof(transactionTime), transactionTime, nameof(prevout), prevout.Hash, prevout.N);

            FetchCoinsResponse coins = this.coinView.FetchCoinsAsync(new[] { prevout.Hash }).GetAwaiter().GetResult();
            if ((coins == null) || (coins.UnspentOutputs.Length != 1))
            {
                this.logger.LogTrace("(-)[READ_PREV_TX_FAILED]");
                ConsensusErrors.ReadTxPrevFailed.Throw();
            }

            ChainedBlock prevBlock = this.chain.GetBlock(coins.BlockHash);
            if (prevBlock == null)
            {
                this.logger.LogTrace("(-)[REORG]");
                ConsensusErrors.ReadTxPrevFailed.Throw();
            }

            UnspentOutputs prevUtxo = coins.UnspentOutputs[0];
            if (this.IsConfirmedInNPrevBlocks(prevUtxo, prevChainedBlock, this.consensusOptions.StakeMinConfirmations - 1))
            {
                this.logger.LogTrace("(-)[LOW_COIN_AGE]");
                ConsensusErrors.InvalidStakeDepth.Throw();
            }

            BlockStake prevBlockStake = this.stakeChain.Get(prevChainedBlock.HashBlock);
            if (prevBlockStake == null)
            {
                this.logger.LogTrace("(-)[BAD_STAKE_BLOCK]");
                ConsensusErrors.BadStakeBlock.Throw();
            }

            this.CheckStakeKernelHash(context, headerBits, prevBlockStake, prevUtxo, prevout, (uint)transactionTime);
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
            {
                //throw new InvalidOperationException("Awful bug, this should never happen");
                array = array.Skip(Math.Abs(missingZero)).ToArray();
            }

            if (missingZero > 0)
                array = new byte[missingZero].Concat(array).ToArray();

            return new uint256(array, false);
        }

        /// <summary>
        /// Converts <see cref="uint256" /> to <see cref="BigInteger" />.
        /// </summary>
        /// <param name="input"><see cref="uint256"/> input value.</param>
        /// <returns><see cref="BigInteger"/> version of <paramref name="input"/>.</returns>
        private BigInteger FromUInt256(uint256 input)
        {
            return BigInteger.Zero;
        }

        /// <summary>
        /// Returns <c>true</c> if provided coins were confirmed in less than <paramref name="targetDepth"/> number of blocks.
        /// </summary>
        /// <param name="coins">Coins to check confirmation depth for.</param>
        /// <param name="referenceChainedBlock">Chained block from which we are counting the depth.</param>
        /// <param name="targetDepth">The target depth.</param>
        /// <returns><c>true</c> if the coins were spent within N blocks from <see cref="referenceChainedBlock"/>, <c>false</c> otherwise.</returns>
        private bool IsConfirmedInNPrevBlocks(UnspentOutputs coins, ChainedBlock referenceChainedBlock, long targetDepth)
        {
            this.logger.LogTrace("({0}:'{1}/{2}',{3}:'{4}',{5}:{6})", nameof(coins), coins.TransactionId, coins.Height, nameof(referenceChainedBlock), referenceChainedBlock, nameof(targetDepth), targetDepth);

            int actualDepth = referenceChainedBlock.Height - (int)coins.Height;
            bool res = actualDepth < targetDepth;

            this.logger.LogTrace("(-):{0}", res);
            return res;
        }

        /// <summary>
        /// Verifies transaction's signature.
        /// </summary>
        /// <param name="coin">UTXO that is spent in the transaction.</param>
        /// <param name="txTo">Transaction.</param>
        /// <param name="txToInN">Index of the transaction's input.</param>
        /// <param name="flagScriptVerify">Script verification flags.</param>
        /// <returns><c>true</c> if signature is valid.</returns>
        private bool VerifySignature(UnspentOutputs coin, Transaction txTo, int txToInN, ScriptVerify flagScriptVerify)
        {
            this.logger.LogTrace("({0}:'{1}/{2}',{3}:{4},{5}:{6})", nameof(coin), coin.TransactionId, coin.Height, nameof(txToInN), txToInN, nameof(flagScriptVerify), flagScriptVerify);

            TxIn input = txTo.Inputs[txToInN];

            if (input.PrevOut.N >= coin.Outputs.Length)
                return false;

            if (input.PrevOut.Hash != coin.TransactionId)
                return false;

            TxOut output = coin.Outputs[input.PrevOut.N];

            var txData = new PrecomputedTransactionData(txTo);
            var checker = new TransactionChecker(txTo, txToInN, output.Value, txData);
            var ctx = new ScriptEvaluationContext(this.chain.Network) { ScriptVerify = flagScriptVerify };

            bool res = ctx.VerifyScript(input.ScriptSig, output.ScriptPubKey, checker);
            this.logger.LogTrace("(-):{0}", res);
            return res;
        }

        /// <summary>
        /// Checks that the stake kernel hash satisfies the target difficulty.
        /// </summary>
        /// <param name="context">Staking context.</param>
        /// <param name="headerBits">Chained block's header bits, which define the difficulty target.</param>
        /// <param name="prevBlockStake">Information about previous staked block.</param>
        /// <param name="stakingCoins">Coins that participate in staking.</param>
        /// <param name="prevout">Information about transaction id and index.</param>
        /// <param name="transactionTime">Transaction time.</param>
        /// <remarks>
        /// Coinstake must meet hash target according to the protocol:
        /// kernel (input 0) must meet the formula
        /// <c>hash(stakeModifierV2 + stakingCoins.Time + prevout.Hash + prevout.N + transactionTime) &lt; target * weight</c>.
        /// This ensures that the chance of getting a coinstake is proportional to the amount of coins one owns.
        /// <para>
        /// The reason this hash is chosen is the following:
        /// <list type="number">
        /// <item><paramref name="prevBlockStake.StakeModifierV2"/>: Scrambles computation to make it very difficult to precompute future proof-of-stake.</item>
        /// <item><paramref name="stakingCoins.Time"/>: Time of the coinstake UTXO. Slightly scrambles computation.</item>
        /// <item><paramref name="prevout.Hash"/> Hash of stakingCoins UTXO, to reduce the chance of nodes generating coinstake at the same time.</item>
        /// <item><paramref name="prevout.N"/>: Output number of stakingCoins UTXO, to reduce the chance of nodes generating coinstake at the same time.</item>
        /// <item><paramref name="transactionTime"/>: Timestamp of the coinstake transaction.</item>
        /// </list>
        /// Block or transaction tx hash should not be used here as they can be generated in vast
        /// quantities so as to generate blocks faster, degrading the system back into a proof-of-work situation.
        /// </para>
        /// </remarks>
        /// <exception cref="ConsensusErrors.StakeTimeViolation">Thrown in case transaction time is lower than it's own UTXO timestamp.</exception>
        /// <exception cref="ConsensusErrors.StakeHashInvalidTarget">Thrown in case PoS hash doesn't meet target protocol.</exception>
        private void CheckStakeKernelHash(ContextStakeInformation context, uint headerBits, BlockStake prevBlockStake, UnspentOutputs stakingCoins,
            OutPoint prevout, uint transactionTime)
        {
            this.logger.LogTrace("({0}:{1:X},{2}.{3}:'{4}',{5}:'{6}/{7}',{8}:'{9}',{10}:{11})",
                nameof(headerBits), headerBits, nameof(prevBlockStake), nameof(prevBlockStake.HashProof), prevBlockStake.HashProof, nameof(stakingCoins),
                stakingCoins.TransactionId, stakingCoins.Height, nameof(prevout), prevout, nameof(transactionTime), transactionTime);

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

            context.TargetProofOfStake = ToUInt256(weightedTarget);
            this.logger.LogTrace("POS target is '{0}', weighted target for {1} coins is '{2}'.", ToUInt256(target), valueIn, context.TargetProofOfStake);

            uint256 stakeModifierV2 = prevBlockStake.StakeModifierV2;

            // Calculate hash.
            using (var ms = new MemoryStream())
            {
                var serializer = new BitcoinStream(ms, true);
                serializer.ReadWrite(stakeModifierV2);
                serializer.ReadWrite(stakingCoins.Time);
                serializer.ReadWrite(prevout.Hash);
                serializer.ReadWrite(prevout.N);
                serializer.ReadWrite(transactionTime);

                context.HashProofOfStake = Hashes.Hash256(ms.ToArray());
            }

            this.logger.LogTrace("Stake modifier V2 is '{0}', hash POS is '{1}'.", stakeModifierV2, context.HashProofOfStake);

            // Now check if proof-of-stake hash meets target protocol.
            var hashProofOfStakeTarget = new BigInteger(1, context.HashProofOfStake.ToBytes(false));
            if (hashProofOfStakeTarget.CompareTo(weightedTarget) > 0)
            {
                this.logger.LogTrace("(-)[TARGET_MISSED]");
                ConsensusErrors.StakeHashInvalidTarget.Throw();
            }

            this.logger.LogTrace("(-)[OK]");
        }
    }
}
