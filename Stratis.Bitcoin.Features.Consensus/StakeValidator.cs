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

        public void CheckProofOfStake(ContextInformation context, ChainedBlock pindexPrev, BlockStake prevBlockStake, Transaction tx, uint nBits)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:'{4}',{5}:{6:X})", nameof(pindexPrev), pindexPrev.HashBlock, nameof(prevBlockStake), nameof(prevBlockStake.HashProof), prevBlockStake.HashProof, nameof(nBits), nBits);

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
            if (IsProtocolV3((int)tx.Time))
            {
                if (this.IsConfirmedInNPrevBlocks(prevUtxo, pindexPrev, this.consensusOptions.StakeMinConfirmations - 1))
                {
                    this.logger.LogTrace("(-)[BAD_STAKE_DEPTH]");
                    ConsensusErrors.InvalidStakeDepth.Throw();
                }
            }
            else
            {
                uint nTimeBlockFrom = prevBlock.Header.Time;
                if (nTimeBlockFrom + this.consensusOptions.StakeMinAge > tx.Time)
                    ConsensusErrors.MinAgeViolation.Throw();
            }

            this.CheckStakeKernelHash(context, pindexPrev, nBits, prevBlock, prevUtxo, prevBlockStake, txIn.PrevOut, tx.Time);

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

        private void CheckStakeKernelHash(ContextInformation context, ChainedBlock pindexPrev, uint nBits, ChainedBlock blockFrom,
            UnspentOutputs txPrev, BlockStake prevBlockStake, OutPoint prevout, uint nTimeTx)
        {
            if (IsProtocolV2(pindexPrev.Height + 1)) this.CheckStakeKernelHashV2(context, pindexPrev, nBits, blockFrom.Header.Time, prevBlockStake, txPrev, prevout, nTimeTx);
            else this.CheckStakeKernelHashV1();
        }

        // Stratis kernel protocol
        // coinstake must meet hash target according to the protocol:
        // kernel (input 0) must meet the formula
        //     hash(nStakeModifier + txPrev.block.nTime + txPrev.nTime + txPrev.vout.hash + txPrev.vout.n + nTime) < bnTarget * nWeight
        // this ensures that the chance of getting a coinstake is proportional to the
        // amount of coins one owns.
        // The reason this hash is chosen is the following:
        //   nStakeModifier: scrambles computation to make it very difficult to precompute
        //                   future proof-of-stake
        //   txPrev.block.nTime: prevent nodes from guessing a good timestamp to
        //                       generate transaction for future advantage,
        //                       obsolete since v3
        //   txPrev.nTime: slightly scrambles computation
        //   txPrev.vout.hash: hash of txPrev, to reduce the chance of nodes
        //                     generating coinstake at the same time
        //   txPrev.vout.n: output number of txPrev, to reduce the chance of nodes
        //                  generating coinstake at the same time
        //   nTime: current timestamp
        //   block/tx hash should not be used here as they can be generated in vast
        //   quantities so as to generate blocks faster, degrading the system back into
        //   a proof-of-work situation.
        //
        private void CheckStakeKernelHashV1()
        {
            // This is not relevant for the stratis blockchain.
            throw new NotImplementedException();
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
        //     hash(nStakeModifier + txPrev.block.nTime + txPrev.nTime + txPrev.vout.hash + txPrev.vout.n + nTime) < bnTarget * nWeight
        // this ensures that the chance of getting a coinstake is proportional to the
        // amount of coins one owns.
        // The reason this hash is chosen is the following:
        //   nStakeModifier: scrambles computation to make it very difficult to precompute
        //                   future proof-of-stake
        //   txPrev.block.nTime: prevent nodes from guessing a good timestamp to
        //                       generate transaction for future advantage,
        //                       obsolete since v3
        //   txPrev.nTime: slightly scrambles computation
        //   txPrev.vout.hash: hash of txPrev, to reduce the chance of nodes
        //                     generating coinstake at the same time
        //   txPrev.vout.n: output number of txPrev, to reduce the chance of nodes
        //                  generating coinstake at the same time
        //   nTime: current timestamp
        //   block/tx hash should not be used here as they can be generated in vast
        //   quantities so as to generate blocks faster, degrading the system back into
        //   a proof-of-work situation.
        //
        private void CheckStakeKernelHashV2(ContextInformation context, ChainedBlock pindexPrev, uint nBits, uint nTimeBlockFrom,
            BlockStake prevBlockStake, UnspentOutputs txPrev, OutPoint prevout, uint nTimeTx)
        {
            this.logger.LogTrace("({0}:'{1}/{2}',{3}:{4:X},{5}:{6},{7}.{8}:'{9}',{10}:'{11}/{12}',{13}:'{14}/{15}',{16}:{17})",
                nameof(pindexPrev), pindexPrev.HashBlock, pindexPrev.Height, nameof(nBits), nBits, nameof(nTimeBlockFrom), nTimeBlockFrom, 
                nameof(prevBlockStake), nameof(prevBlockStake.HashProof), prevBlockStake.HashProof, nameof(txPrev), txPrev.TransactionId, txPrev.Height,
                nameof(prevout), prevout.Hash, prevout.N, nameof(nTimeTx), nTimeTx);

            if (nTimeTx < txPrev.Time)
            {
                this.logger.LogTrace("Coinstake transaction timestamp {0} is lower than its own UTXO timestamp {1}.", nTimeTx, txPrev.Time);
                this.logger.LogTrace("(-)[BAD_STAKE_TIME]");
                ConsensusErrors.StakeTimeViolation.Throw();
            }

            // Base target.
            BigInteger bnTarget = new Target(nBits).ToBigInteger();

            // TODO: Investigate:
            // The POS protocol should probably put a limit on the max amount that can be staked
            // not a hard limit but a limit that allow any amount to be staked with a max weight value.
            // the max weight should not exceed the max uint256 array size (array size = 32).

            // Weighted target.
            long nValueIn = txPrev._Outputs[prevout.N].Value.Satoshi;
            BigInteger bnWeight = BigInteger.ValueOf(nValueIn);
            BigInteger bnWeightedTarget = bnTarget.Multiply(bnWeight);

            context.Stake.TargetProofOfStake = ToUInt256(bnWeightedTarget);
            this.logger.LogTrace("POS target is '{0}', weighted target for {1} coins is '{2}'.", ToUInt256(bnTarget), nValueIn, context.Stake.TargetProofOfStake);

            ulong nStakeModifier = prevBlockStake.StakeModifier; //pindexPrev.Header.BlockStake.StakeModifier;
            uint256 bnStakeModifierV2 = prevBlockStake.StakeModifierV2; //pindexPrev.Header.BlockStake.StakeModifierV2;
            int nStakeModifierHeight = pindexPrev.Height;
            uint nStakeModifierTime = pindexPrev.Header.Time;

            // Calculate hash
            using (var ms = new MemoryStream())
            {
                var serializer = new BitcoinStream(ms, true);
                if (IsProtocolV3((int)nTimeTx))
                {
                    serializer.ReadWrite(bnStakeModifierV2);
                }
                else
                {
                    serializer.ReadWrite(nStakeModifier);
                    serializer.ReadWrite(nTimeBlockFrom);
                }

                serializer.ReadWrite(txPrev.Time);
                serializer.ReadWrite(prevout.Hash);
                serializer.ReadWrite(prevout.N);
                serializer.ReadWrite(nTimeTx);

                context.Stake.HashProofOfStake = Hashes.Hash256(ms.ToArray());
            }

            this.logger.LogTrace("Stake modifiers are {0} and '{1}', hash POS is '{2}'.", nStakeModifier, bnStakeModifierV2, context.Stake.HashProofOfStake);

            //LogPrintf("CheckStakeKernelHash() : using modifier 0x%016x at height=%d timestamp=%s for block from timestamp=%s\n",
            //    nStakeModifier, nStakeModifierHeight,
            //    DateTimeStrFormat(nStakeModifierTime),

            //    DateTimeStrFormat(nTimeBlockFrom));

            //LogPrintf("CheckStakeKernelHash() : check modifier=0x%016x nTimeBlockFrom=%u nTimeTxPrev=%u nPrevout=%u nTimeTx=%u hashProof=%s\n",
            //    nStakeModifier,
            //    nTimeBlockFrom, txPrev.nTime, prevout.n, nTimeTx,
            //    hashProofOfStake.ToString());

            // Now check if proof-of-stake hash meets target protocol.
            BigInteger hashProofOfStakeTarget = new BigInteger(1, context.Stake.HashProofOfStake.ToBytes(false));
            if (hashProofOfStakeTarget.CompareTo(bnWeightedTarget) > 0)
            {
                this.logger.LogTrace("(-)[TARGET_MISSED]");
                ConsensusErrors.StakeHashInvalidTarget.Throw();
            }

            //  if (fDebug && !fPrintProofOfStake)
            //  {
            //        LogPrintf("CheckStakeKernelHash() : using modifier 0x%016x at height=%d timestamp=%s for block from timestamp=%s\n",
            //        nStakeModifier, nStakeModifierHeight,
            //        DateTimeStrFormat(nStakeModifierTime),

            //        DateTimeStrFormat(nTimeBlockFrom));

            //        LogPrintf("CheckStakeKernelHash() : pass modifier=0x%016x nTimeBlockFrom=%u nTimeTxPrev=%u nPrevout=%u nTimeTx=%u hashProof=%s\n",
            //        nStakeModifier,
            //        nTimeBlockFrom, txPrev.nTime, prevout.n, nTimeTx,
            //        hashProofOfStake.ToString());
            //  }
            this.logger.LogTrace("(-)");
        }

        public void ComputeStakeModifier(ChainBase chainIndex, ChainedBlock pindex, BlockStake blockStake)
        {
            this.logger.LogTrace("({0}:'{1}/{2}',{3}.{4}:'{5}')", nameof(pindex), pindex.HashBlock, pindex.Height, nameof(blockStake), nameof(blockStake.HashProof), blockStake.HashProof);

            ChainedBlock pindexPrev = pindex.Previous;
            BlockStake blockStakePrev = pindexPrev == null ? null : this.stakeChain.Get(pindexPrev.HashBlock);

            // compute stake modifier
            var stakeContext = new StakeModifierContext();
            this.ComputeNextStakeModifier(chainIndex, pindexPrev, stakeContext);

            blockStake.SetStakeModifier(stakeContext.StakeModifier, stakeContext.GeneratedStakeModifier);
            blockStake.StakeModifierV2 = this.ComputeStakeModifierV2(pindexPrev, blockStakePrev, blockStake.IsProofOfWork() ? pindex.HashBlock : blockStake.PrevoutStake.Hash);

            this.logger.LogTrace("(-):{0}=0x{1:x},{2}='{3}'", nameof(blockStake.StakeModifier), blockStake.StakeModifier, nameof(blockStake.StakeModifierV2), blockStake.StakeModifierV2);
        }

        // Stake Modifier (hash modifier of proof-of-stake):
        // The purpose of stake modifier is to prevent a txout (coin) owner from
        // computing future proof-of-stake generated by this txout at the time
        // of transaction confirmation. To meet kernel protocol, the txout
        // must hash with a future stake modifier to generate the proof.
        // Stake modifier consists of bits each of which is contributed from a
        // selected block of a given block group in the past.
        // The selection of a block is based on a hash of the block's proof-hash and
        // the previous stake modifier.
        // Stake modifier is recomputed at a fixed time interval instead of every 
        // block. This is to make it difficult for an attacker to gain control of
        // additional bits in the stake modifier, even after generating a chain of
        // blocks.
        public void ComputeNextStakeModifier(ChainBase chainIndex, ChainedBlock pindexPrev, StakeModifierContext stakeModifier)
        {
            stakeModifier.StakeModifier = 0;
            stakeModifier.GeneratedStakeModifier = false;
            if (pindexPrev == null)
            {
                stakeModifier.GeneratedStakeModifier = true;
                return; // Genesis block's modifier is 0.
            }

            // First find current stake modifier and its generation block time
            // if it's not old enough, return the same stake modifier.
            if (!this.GetLastStakeModifier(pindexPrev, stakeModifier))
                ConsensusErrors.ModifierNotFound.Throw();

            if ((stakeModifier.ModifierTime / this.consensusOptions.StakeModifierInterval) >= (pindexPrev.Header.Time / this.consensusOptions.StakeModifierInterval))
                return;

            // Sort candidate blocks by timestamp.
            var sortedByTimestamp = new SortedDictionary<uint, ChainedBlock>();
            long nSelectionInterval = this.GetStakeModifierSelectionInterval();
            long nSelectionIntervalStart = (pindexPrev.Header.Time / this.consensusOptions.StakeModifierInterval) * this.consensusOptions.StakeModifierInterval - nSelectionInterval;
            ChainedBlock pindex = pindexPrev;
            while ((pindex != null) && (pindex.Header.Time >= nSelectionIntervalStart))
            {
                sortedByTimestamp.Add(pindex.Header.Time, pindex);
                pindex = pindex.Previous;
            }
            //int nHeightFirstCandidate = pindex?.Height + 1 ?? 0;

            // Select 64 blocks from candidate blocks to generate stake modifier
            ulong nStakeModifierNew = 0;
            long nSelectionIntervalStop = nSelectionIntervalStart;
            var mapSelectedBlocks = new Dictionary<uint256, ChainedBlock>();
            int counter = sortedByTimestamp.Count;
            ChainedBlock[] sorted = sortedByTimestamp.Values.ToArray();
            for (int nRound = 0; nRound < Math.Min(64, counter); nRound++)
            {
                // add an interval section to the current selection round
                nSelectionIntervalStop += this.GetStakeModifierSelectionIntervalSection(nRound);

                // Select a block from the candidates of current round.
                BlockStake blockStake;
                if (!this.SelectBlockFromCandidates(sorted, mapSelectedBlocks, nSelectionIntervalStop, stakeModifier.StakeModifier, out pindex, out blockStake))
                    ConsensusErrors.FailedSelectBlock.Throw();

                // Write the entropy bit of the selected block.
                nStakeModifierNew |= ((ulong)blockStake.GetStakeEntropyBit() << nRound);

                // Add the selected block from candidates to selected list.
                mapSelectedBlocks.Add(pindex.HashBlock, pindex);

                //LogPrint("stakemodifier", "ComputeNextStakeModifier: selected round %d stop=%s height=%d bit=%d\n", nRound, DateTimeStrFormat(nSelectionIntervalStop), pindex->nHeight, pindex->GetStakeEntropyBit());
            }

            //  // Print selection map for visualization of the selected blocks
            //  if (LogAcceptCategory("stakemodifier"))
            //  {
            //      string strSelectionMap = "";
            //      '-' indicates proof-of-work blocks not selected
            //      strSelectionMap.insert(0, pindexPrev->nHeight - nHeightFirstCandidate + 1, '-');
            //      pindex = pindexPrev;
            //      while (pindex && pindex->nHeight >= nHeightFirstCandidate)
            //      {
            //          // '=' indicates proof-of-stake blocks not selected
            //          if (pindex->IsProofOfStake())
            //              strSelectionMap.replace(pindex->nHeight - nHeightFirstCandidate, 1, "=");
            //          pindex = pindex->pprev;
            //      }

            //      BOOST_FOREACH(const PAIRTYPE(uint256, const CBlockIndex*)& item, mapSelectedBlocks)
            //      {
            //          // 'S' indicates selected proof-of-stake blocks
            //          // 'W' indicates selected proof-of-work blocks
            //          strSelectionMap.replace(item.second->nHeight - nHeightFirstCandidate, 1, item.second->IsProofOfStake()? "S" : "W");
            //      }

            //      LogPrintf("ComputeNextStakeModifier: selection height [%d, %d] map %s\n", nHeightFirstCandidate, pindexPrev->nHeight, strSelectionMap);
            //  }

            //LogPrint("stakemodifier", "ComputeNextStakeModifier: new modifier=0x%016x time=%s\n", nStakeModifierNew, DateTimeStrFormat(pindexPrev->GetBlockTime()));

            stakeModifier.StakeModifier = nStakeModifierNew;
            stakeModifier.GeneratedStakeModifier = true;
        }

        // Stake Modifier (hash modifier of proof-of-stake):
        // The purpose of stake modifier is to prevent a txout (coin) owner from
        // computing future proof-of-stake generated by this txout at the time
        // of transaction confirmation. To meet kernel protocol, the txout
        // must hash with a future stake modifier to generate the proof.
        public uint256 ComputeStakeModifierV2(ChainedBlock pindexPrev, BlockStake blockStakePrev, uint256 kernel)
        {
            if (pindexPrev == null)
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

        // Get the last stake modifier and its generation time from a given block.
        private bool GetLastStakeModifier(ChainedBlock pindex, StakeModifierContext stakeModifier)
        {
            stakeModifier.StakeModifier = 0;
            stakeModifier.ModifierTime = 0;

            if (pindex == null)
                return false;

            BlockStake blockStake = this.stakeChain.Get(pindex.HashBlock);
            while ((pindex != null) && (pindex.Previous != null) && !blockStake.GeneratedStakeModifier())
            {
                pindex = pindex.Previous;
                blockStake = this.stakeChain.Get(pindex.HashBlock);
            }

            if (!blockStake.GeneratedStakeModifier())
                return false; // error("GetLastStakeModifier: no generation at genesis block");

            stakeModifier.StakeModifier = blockStake.StakeModifier;
            stakeModifier.ModifierTime = pindex.Header.Time;

            return true;
        }

        // Get stake modifier selection interval (in seconds).
        private long GetStakeModifierSelectionInterval()
        {
            long nSelectionInterval = 0;
            for (int nSection = 0; nSection < 64; nSection++)
                nSelectionInterval += this.GetStakeModifierSelectionIntervalSection(nSection);

            return nSelectionInterval;
        }

        // Get selection interval section (in seconds).
        private long GetStakeModifierSelectionIntervalSection(int nSection)
        {
            if (!((nSection >= 0) && (nSection < 64)))
                throw new ArgumentOutOfRangeException();

            return (this.consensusOptions.StakeModifierInterval * 63 / (63 + ((63 - nSection) * (ModifierIntervalRatio - 1))));
        }

        // Select a block from the candidate blocks in vSortedByTimestamp, excluding
        // already selected blocks in vSelectedBlocks, and with timestamp up to
        // nSelectionIntervalStop.
        private bool SelectBlockFromCandidates(ChainedBlock[] sortedByTimestamp, Dictionary<uint256, ChainedBlock> mapSelectedBlocks,
            long nSelectionIntervalStop, ulong nStakeModifierPrev, out ChainedBlock pindexSelected, out BlockStake blockStakeSelected)
        {
            bool fSelected = false;
            uint256 hashBest = 0;
            pindexSelected = null;
            blockStakeSelected = null;

            for (int i = 0; i < sortedByTimestamp.Length; i++)
            {
                ChainedBlock pindex = sortedByTimestamp[i];

                if (fSelected && (pindex.Header.Time > nSelectionIntervalStop))
                    break;

                if (mapSelectedBlocks.ContainsKey(pindex.HashBlock))
                    continue;

                BlockStake blockStake = this.stakeChain.Get(pindex.HashBlock);

                // Compute the selection hash by hashing its proof-hash and the
                // previous proof-of-stake modifier.
                uint256 hashSelection;
                using (var ms = new MemoryStream())
                {
                    var serializer = new BitcoinStream(ms, true);
                    serializer.ReadWrite(blockStake.HashProof);
                    serializer.ReadWrite(nStakeModifierPrev);

                    hashSelection = Hashes.Hash256(ms.ToArray());
                }

                // The selection hash is divided by 2**32 so that proof-of-stake block
                // is always favored over proof-of-work block. this is to preserve
                // the energy efficiency property.
                if (blockStake.IsProofOfStake())
                    hashSelection >>= 32;

                if (fSelected && (hashSelection < hashBest))
                {
                    hashBest = hashSelection;
                    pindexSelected = pindex;
                    blockStakeSelected = blockStake;
                }
                else if (!fSelected)
                {
                    fSelected = true;
                    hashBest = hashSelection;
                    pindexSelected = pindex;
                    blockStakeSelected = blockStake;
                }
            }

            //LogPrint("stakemodifier", "SelectBlockFromCandidates: selection hash=%s\n", hashBest.ToString());
            return fSelected;
        }

        // ppcoin: Total coin age spent in transaction, in the unit of coin-days.
        // Only those coins meeting minimum age requirement counts. As those
        // transactions not in main chain are not currently indexed so we
        // might not find out about their coin age. Older transactions are 
        // guaranteed to be in main chain by sync-checkpoint. This rule is
        // introduced to help nodes establish a consistent view of the coin
        // age (trust score) of competing branches.
        public bool GetCoinAge(ConcurrentChain chain, CoinView coinView, Transaction trx, ChainedBlock pindexPrev, out ulong nCoinAge)
        {
            BigInteger bnCentSecond = BigInteger.Zero;  // coin age in the unit of cent-seconds
            nCoinAge = 0;

            if (trx.IsCoinBase)
                return true;

            foreach (TxIn txin in trx.Inputs)
            {
                FetchCoinsResponse coins = coinView.FetchCoinsAsync(new[] { txin.PrevOut.Hash }).GetAwaiter().GetResult();
                if ((coins == null) || (coins.UnspentOutputs.Length != 1))
                    continue;

                ChainedBlock prevBlock = chain.GetBlock(coins.BlockHash);
                UnspentOutputs prevUtxo = coins.UnspentOutputs[0];

                // First try finding the previous transaction in database
                // Transaction txPrev = trasnactionStore.Get(txin.PrevOut.Hash);
                // if (txPrev == null)
                //     continue;  // previous transaction not in main chain
                if (trx.Time < prevUtxo.Time)
                    return false;  // Transaction timestamp violation.

                if (IsProtocolV3((int)trx.Time))
                {
                    if (this.IsConfirmedInNPrevBlocks(prevUtxo, pindexPrev, this.consensusOptions.StakeMinConfirmations - 1))
                    {
                        //LogPrint("coinage", "coin age skip nSpendDepth=%d\n", nSpendDepth + 1);
                        continue; // only count coins meeting min confirmations requirement
                    }
                }
                else
                {
                    // Read block header
                    //var block = blockStore.GetBlock(txPrev.GetHash());
                    //if (block == null)
                    //    return false; // unable to read block of previous transaction
                    if (prevBlock.Header.Time + this.consensusOptions.StakeMinAge > trx.Time)
                        continue; // only count coins meeting min age requirement
                }

                long nValueIn = prevUtxo._Outputs[txin.PrevOut.N].Value;
                var multiplier = BigInteger.ValueOf((trx.Time - prevUtxo.Time) / Money.CENT);
                bnCentSecond = bnCentSecond.Add(BigInteger.ValueOf(nValueIn).Multiply(multiplier));
                //bnCentSecond += new BigInteger(nValueIn) * (trx.Time - txPrev.Time) / CENT;

                //LogPrint("coinage", "coin age nValueIn=%d nTimeDiff=%d bnCentSecond=%s\n", nValueIn, nTime - txPrev.nTime, bnCentSecond.ToString());
            }

            BigInteger bnCoinDay = bnCentSecond.Multiply(BigInteger.ValueOf(Money.CENT / Money.COIN / (24 * 60 * 60)));
            //BigInteger bnCoinDay = bnCentSecond * CENT / COIN / (24 * 60 * 60);

            //LogPrint("coinage", "coin age bnCoinDay=%s\n", bnCoinDay.ToString());
            nCoinAge = new Target(bnCoinDay).ToCompact();

            return true;
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
            if (IsProtocolV3((int)nTime))
            {
                if (this.IsConfirmedInNPrevBlocks(prevUtxo, pindexPrev, this.consensusOptions.StakeMinConfirmations - 1))
                {
                    this.logger.LogTrace("(-)[LOW_COIN_AGE]");
                    ConsensusErrors.InvalidStakeDepth.Throw();
                }
            }
            else
            {
                uint nTimeBlockFrom = prevBlock.Header.Time;
                if (nTimeBlockFrom + this.consensusOptions.StakeMinAge > nTime)
                    ConsensusErrors.MinAgeViolation.Throw();
            }

            BlockStake prevBlockStake = this.stakeChain.Get(pindexPrev.HashBlock);
            if (prevBlockStake == null)
            {
                this.logger.LogTrace("(-)[BAD_STAKE_BLOCK]");
                ConsensusErrors.BadStakeBlock.Throw();
            }

            pBlockTime = prevBlock.Header.Time;

            this.CheckStakeKernelHash(context, pindexPrev, nBits, prevBlock, prevUtxo, prevBlockStake, prevout, (uint)nTime);
            this.logger.LogTrace("(-):{0}={1}", nameof(pBlockTime), pBlockTime);
        }

        public static bool IsProtocolV2(int height)
        {
            return height > 0;
        }

        public static bool IsProtocolV3(int nTime)
        {
            return nTime > 1470467000;
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
            if (IsProtocolV1RetargetingFixed(indexLast.Height) && (actualSpacing < 0))
                actualSpacing = targetSpacing;

            if (IsProtocolV3((int)indexLast.Header.Time) && (actualSpacing > targetSpacing * 10))
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
            return IsProtocolV2(height) ? consensus.ProofOfStakeLimitV2 : consensus.ProofOfStakeLimit;
        }

        public static int GetTargetSpacing(int height)
        {
            return IsProtocolV2(height) ? 64 : 60;
        }

        private static bool IsProtocolV1RetargetingFixed(int height)
        {
            return height > 0;
        }

        public static uint GetPastTimeLimit(ChainedBlock chainedBlock)
        {
            return IsProtocolV2(chainedBlock.Height) ? chainedBlock.Header.Time : GetMedianTimePast(chainedBlock);
        }

        public static uint GetMedianTimePast(ChainedBlock chainedBlock)
        {
            var sortedList = new SortedSet<uint>();
            ChainedBlock pindex = chainedBlock;
            for (int i = 0; (i < MedianTimeSpan) && (pindex != null); i++, pindex = pindex.Previous)
                sortedList.Add(pindex.Header.Time);

            return (sortedList.First() - sortedList.Last()) / 2;
        }
    }
}
