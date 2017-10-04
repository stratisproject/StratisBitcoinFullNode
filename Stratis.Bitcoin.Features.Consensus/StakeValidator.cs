using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using NLog.Extensions.Logging;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class StakeValidator
    {
        public class StakeModifierContext
        {
            public ulong StakeModifier { get; set; }
            public bool GeneratedStakeModifier { get; set; }
            public long ModifierTime { get; set; }
        }

        // Ratio of group interval length between the last group and the first group.
        private const int ModifierIntervalRatio = 3;

        private const int MedianTimeSpan = 11;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Class logger.</summary>
        private static readonly ILogger clogger;

        private readonly Network network;
        private readonly StakeChain stakeChain;
        private readonly ConcurrentChain chain;
        private readonly CoinView coinView;
        private readonly PosConsensusOptions consensusOptions;

        static StakeValidator()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddNLog();
            clogger = loggerFactory.CreateLogger(typeof(StakeValidator).FullName);
        }

        public StakeValidator(Network network, StakeChain stakeChain, ConcurrentChain chain, CoinView coinView, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.stakeChain = stakeChain;
            this.chain = chain;
            this.coinView = coinView;
            this.consensusOptions = network.Consensus.Option<PosConsensusOptions>();
        }

        public void CheckProofOfStake(ContextInformation context, ChainedBlock pindexPrev, BlockStake prevBlockStake, Transaction tx, uint headerBits)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:'{4}',{5}:0x{6:X})", nameof(pindexPrev), pindexPrev.HashBlock, nameof(prevBlockStake), nameof(prevBlockStake.HashProof), prevBlockStake.HashProof, nameof(headerBits), headerBits);

            if (!tx.IsCoinStake)
            {
                this.logger.LogTrace("(-)[NO_COINSTAKE]");
                ConsensusErrors.NonCoinstake.Throw();
            }

            // Kernel (input 0) must match the stake hash target per coin age (nBits).
            TxIn txIn = tx.Inputs[0];

            // First try finding the previous transaction in database.
            FetchCoinsResponse coins = this.coinView.FetchCoinsAsync(new[] { txIn.PrevOut.Hash }).GetAwaiter().GetResult();
            if ((coins == null) || (coins.UnspentOutputs.Length != 1))
                ConsensusErrors.ReadTxPrevFailed.Throw();

            ChainedBlock prevBlock = this.chain.GetBlock(coins.BlockHash);
            UnspentOutputs prevUtxo = coins.UnspentOutputs[0];

            // Verify signature.
            if (!this.VerifySignature(prevUtxo, tx, 0, ScriptVerify.None))
            {
                this.logger.LogTrace("(-)[BAD_SIGNATURE]");
                ConsensusErrors.CoinstakeVerifySignatureFailed.Throw();
            }

            // Min age requirement.
            if (this.IsConfirmedInNPrevBlocks(prevUtxo, pindexPrev, this.consensusOptions.StakeMinConfirmations - 1))
            {
                this.logger.LogTrace("(-)[BAD_STAKE_DEPTH]");
                ConsensusErrors.InvalidStakeDepth.Throw();
            }

            this.CheckStakeKernelHash(context, pindexPrev, headerBits, prevBlock.Header.Time, prevBlockStake, prevUtxo, txIn.PrevOut, tx.Time);

            this.logger.LogTrace("(-)[OK]");
        }

        private bool IsConfirmedInNPrevBlocks(UnspentOutputs utxoSet, ChainedBlock pindexFrom, long maxDepth)
        {
            this.logger.LogTrace("({0}:'{1}/{2}',{3}:'{4}/{5}',{6}:{7})", nameof(utxoSet), utxoSet.TransactionId, utxoSet.Height, nameof(pindexFrom), pindexFrom.HashBlock, pindexFrom.Height, nameof(maxDepth), maxDepth);
            
            int actualDepth = pindexFrom.Height - (int)utxoSet.Height;
            bool res = actualDepth < maxDepth;

            this.logger.LogTrace("(-):{0}", res);
            return res;
        }

        private bool VerifySignature(UnspentOutputs txFrom, Transaction txTo, int txToInN, ScriptVerify flagScriptVerify)
        {
            this.logger.LogTrace("({0}:'{1}/{2}',{3}:{4},{5}:{6})", nameof(txFrom), txFrom.TransactionId, txFrom.Height, nameof(txToInN), txToInN, nameof(flagScriptVerify), flagScriptVerify);

            TxIn input = txTo.Inputs[txToInN];

            if (input.PrevOut.N >= txFrom._Outputs.Length)
                return false;

            if (input.PrevOut.Hash != txFrom.TransactionId)
                return false;

            TxOut output = txFrom._Outputs[input.PrevOut.N];

            var txData = new PrecomputedTransactionData(txTo);
            var checker = new TransactionChecker(txTo, txToInN, output.Value, txData);
            var ctx = new ScriptEvaluationContext { ScriptVerify = flagScriptVerify };

            bool res = ctx.VerifyScript(input.ScriptSig, output.ScriptPubKey, checker);
            this.logger.LogTrace("(-):{0}", res);
            return res;
        }

        private static uint256 ToUInt256(BigInteger input)
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

        private static BigInteger FromUInt256(uint256 input)
        {
            return BigInteger.Zero;
        }

        // Stratis kernel protocol
        // coinstake must meet hash target according to the protocol:
        // kernel (input 0) must meet the formula
        //     hash(stakeModifierV2 + stakingCoins.Time + prevout.Hash + prevout.N + transactionTime) < target * weight
        // this ensures that the chance of getting a coinstake is proportional to the
        // amount of coins one owns.
        // The reason this hash is chosen is the following:
        //   stakeModifierV2: Scrambles computation to make it very difficult to precompute future proof-of-stake.
        //   stakingCoins.Time: Time of the coinstake UTXO. Slightly scrambles computation.
        //   prevout.Hash: Hash of stakingCoins UTXO, to reduce the chance of nodes generating coinstake at the same time.
        //   prevout.N: Output number of stakingCoins UTXO, to reduce the chance of nodes generating coinstake at the same time.
        //   transactionTime: Timestamp of the coinstake transaction.
        //   Block or transaction tx hash should not be used here as they can be generated in vast
        //   quantities so as to generate blocks faster, degrading the system back into a proof-of-work situation.
        //
        private void CheckStakeKernelHash(ContextInformation context, ChainedBlock prevChainedBlock, uint headerBits, uint prevBlockTime,
            BlockStake prevBlockStake, UnspentOutputs stakingCoins, OutPoint prevout, uint transactionTime)
        {
            this.logger.LogTrace("({0}:'{1}/{2}',{3}:{4:X},{5}:{6},{7}.{8}:'{9}',{10}:'{11}/{12}',{13}:'{14}/{15}',{16}:{17})",
                nameof(prevChainedBlock), prevChainedBlock.HashBlock, prevChainedBlock.Height, nameof(headerBits), headerBits, nameof(prevBlockTime), prevBlockTime, 
                nameof(prevBlockStake), nameof(prevBlockStake.HashProof), prevBlockStake.HashProof, nameof(stakingCoins), stakingCoins.TransactionId, stakingCoins.Height,
                nameof(prevout), prevout.Hash, prevout.N, nameof(transactionTime), transactionTime);

            if (transactionTime < stakingCoins.Time)
            {
                this.logger.LogTrace("Coinstake transaction timestamp {0} is lower than its own UTXO timestamp {1}.", transactionTime, stakingCoins.Time);
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
            long valueIn = stakingCoins._Outputs[prevout.N].Value.Satoshi;
            BigInteger weight = BigInteger.ValueOf(valueIn);
            BigInteger weightedTarget = target.Multiply(weight);

            context.Stake.TargetProofOfStake = ToUInt256(weightedTarget);
            this.logger.LogTrace("POS target is '{0}', weighted target for {1} coins is '{2}'.", ToUInt256(target), valueIn, context.Stake.TargetProofOfStake);

            uint256 stakeModifierV2 = prevBlockStake.StakeModifierV2;

            // Calculate hash
            using (var ms = new MemoryStream())
            {
                var serializer = new BitcoinStream(ms, true);
                serializer.ReadWrite(stakeModifierV2);
                serializer.ReadWrite(stakingCoins.Time);
                serializer.ReadWrite(prevout.Hash);
                serializer.ReadWrite(prevout.N);
                serializer.ReadWrite(transactionTime);

                context.Stake.HashProofOfStake = Hashes.Hash256(ms.ToArray());
            }

            this.logger.LogTrace("Stake modifier V2 is '{0}', hash POS is '{1}'.", stakeModifierV2, context.Stake.HashProofOfStake);

            // Now check if proof-of-stake hash meets target protocol.
            BigInteger hashProofOfStakeTarget = new BigInteger(1, context.Stake.HashProofOfStake.ToBytes(false));
            if (hashProofOfStakeTarget.CompareTo(weightedTarget) > 0)
            {
                this.logger.LogTrace("(-)[TARGET_MISSED]");
                ConsensusErrors.StakeHashInvalidTarget.Throw();
            }

            this.logger.LogTrace("(-)");
        }

        // Stake Modifier (hash modifier of proof-of-stake):
        // The purpose of stake modifier is to prevent a txout (coin) owner from
        // computing future proof-of-stake generated by this txout at the time
        // of transaction confirmation. To meet kernel protocol, the txout
        // must hash with a future stake modifier to generate the proof.
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

        public void CheckKernel(ContextInformation context, ChainedBlock pindexPrev, uint nBits, long nTime, OutPoint prevout, ref long pBlockTime)
        {
            this.logger.LogTrace("({0}:'{1}/{2}',{3}:{4:X},{5}:{6},{7}:'{8}.{9}')", nameof(pindexPrev), pindexPrev.HashBlock, pindexPrev.Height, 
                nameof(nBits), nBits, nameof(nTime), nTime, nameof(prevout), prevout.Hash, prevout.N);

            // TODO: https://github.com/stratisproject/StratisBitcoinFullNode/issues/397
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
            if (this.IsConfirmedInNPrevBlocks(prevUtxo, pindexPrev, this.consensusOptions.StakeMinConfirmations - 1))
            {
                this.logger.LogTrace("(-)[LOW_COIN_AGE]");
                ConsensusErrors.InvalidStakeDepth.Throw();
            }

            BlockStake prevBlockStake = this.stakeChain.Get(pindexPrev.HashBlock);
            if (prevBlockStake == null)
            {
                this.logger.LogTrace("(-)[BAD_STAKE_BLOCK]");
                ConsensusErrors.BadStakeBlock.Throw();
            }

            pBlockTime = prevBlock.Header.Time;

            this.CheckStakeKernelHash(context, pindexPrev, nBits, prevBlock.Header.Time, prevBlockStake, prevUtxo, prevout, (uint)nTime);

            this.logger.LogTrace("(-):{0}={1}", nameof(pBlockTime), pBlockTime);
        }

        public static ChainedBlock GetLastBlockIndex(StakeChain stakeChain, ChainedBlock index, bool proofOfStake)
        {
            if (index == null)
                throw new ArgumentNullException(nameof(index));

            clogger.LogTrace("({0}:'{1}/{2}',{3}:{4})", nameof(index), index.HashBlock, index.Height, nameof(proofOfStake), proofOfStake);

            BlockStake blockStake = stakeChain.Get(index.HashBlock);

            while ((index.Previous != null) && (blockStake.IsProofOfStake() != proofOfStake))
            {
                index = index.Previous;
                blockStake = stakeChain.Get(index.HashBlock);
            }

            clogger.LogTrace("(-)':{0}'", index);
            return index;
        }

        public static Target GetNextTargetRequired(StakeChain stakeChain, ChainedBlock indexLast, NBitcoin.Consensus consensus, bool proofOfStake)
        {
            clogger.LogTrace("({0}:'{1}/{2}',{3}:{4})", nameof(indexLast), indexLast?.HashBlock, indexLast?.Height, nameof(proofOfStake), proofOfStake);

            // Genesis block.
            if (indexLast == null)
            {
                clogger.LogTrace("(-)[GENESIS]:'{0}'", consensus.PowLimit);
                return consensus.PowLimit;
            }

            // Find the last two blocks that correspond to the mining algo 
            // (i.e if this is a POS block we need to find the last two POS blocks).
            BigInteger targetLimit = proofOfStake
                ? GetProofOfStakeLimit(consensus, indexLast.Height)
                : consensus.PowLimit.ToBigInteger();

            // First block.
            ChainedBlock pindexPrev = GetLastBlockIndex(stakeChain, indexLast, proofOfStake);
            if (pindexPrev.Previous == null)
            {
                var res = new Target(targetLimit);
                clogger.LogTrace("(-)[FIRST_BLOCK]:'{0}'", res);
                return res;
            }

            // Second block.
            ChainedBlock pindexPrevPrev = GetLastBlockIndex(stakeChain, pindexPrev.Previous, proofOfStake);
            if (pindexPrevPrev.Previous == null)
            {
                var res = new Target(targetLimit);
                clogger.LogTrace("(-)[SECOND_BLOCK]:'{0}'", res);
                return res;
            }

            int targetSpacing = GetTargetSpacing(indexLast.Height);
            int actualSpacing = (int)(pindexPrev.Header.Time - pindexPrevPrev.Header.Time);
            if (actualSpacing < 0)
                actualSpacing = targetSpacing;

            if (actualSpacing > targetSpacing * 10)
                actualSpacing = targetSpacing * 10;

            // Target change every block
            // retarget with exponential moving toward target spacing.
            int targetTimespan = 16 * 60; // 16 mins.
            BigInteger target = pindexPrev.Header.Bits.ToBigInteger();

            int interval = targetTimespan / targetSpacing;
            target = target.Multiply(BigInteger.ValueOf(((interval - 1) * targetSpacing + actualSpacing + actualSpacing)));
            target = target.Divide(BigInteger.ValueOf(((interval + 1) * targetSpacing)));

            if ((target.CompareTo(BigInteger.Zero) <= 0) || (target.CompareTo(targetLimit) >= 1))
                target = targetLimit;

            var finalTarget = new Target(target);
            clogger.LogTrace("(-):'{0}'", finalTarget);
            return finalTarget;
        }

        private static BigInteger GetProofOfStakeLimit(NBitcoin.Consensus consensus, int height)
        {
            return consensus.ProofOfStakeLimitV2;
        }

        public static int GetTargetSpacing(int height)
        {
            return 64;
        }
    }
}
