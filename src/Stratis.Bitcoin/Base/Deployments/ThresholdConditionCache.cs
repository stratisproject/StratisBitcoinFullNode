using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base.Deployments
{
    public class ThresholdConditionCache
    {
        /** What block version to use for new blocks (pre versionbits) */
        public static int VERSIONBITS_LAST_OLD_BLOCK_VERSION = 4;
        /** What bits to set in version for versionbits blocks */
        public static uint VERSIONBITS_TOP_BITS = 0x20000000;
        /** What bitmask determines whether versionbits is in use */
        public static uint VERSIONBITS_TOP_MASK = 0xE0000000;
        /** Total bits available for versionbits */
        public static int VERSIONBITS_NUM_BITS = 29;

        public static readonly int ArraySize;
        static ThresholdConditionCache()
        {
            ArraySize = Enum.GetValues(typeof(BIP9Deployments)).Length;
        }

        NBitcoin.Consensus _Consensus;
        Dictionary<uint256, ThresholdState?[]> cache = new Dictionary<uint256, ThresholdState?[]>();

        public ThresholdConditionCache(NBitcoin.Consensus consensus)
        {
            Guard.NotNull(consensus, nameof(consensus));

            this._Consensus = consensus;
        }

        public ThresholdState[] GetStates(ChainedBlock pindexPrev)
        {
            return Enum.GetValues(typeof(BIP9Deployments))
                .OfType<BIP9Deployments>()
                .Select(b => this.GetState(pindexPrev, b))
                .ToArray();
        }

        public ThresholdState GetState(ChainedBlock pindexPrev, BIP9Deployments deployment)
        {
            int nPeriod = this._Consensus.MinerConfirmationWindow;
            int nThreshold = this._Consensus.RuleChangeActivationThreshold;
            var nTimeStart = this._Consensus.BIP9Deployments[deployment]?.StartTime;
            var nTimeTimeout = this._Consensus.BIP9Deployments[deployment]?.Timeout;

            // A block's state is always the same as that of the first of its period, so it is computed based on a pindexPrev whose height equals a multiple of nPeriod - 1.
            if(pindexPrev != null)
            {
                pindexPrev = pindexPrev.GetAncestor(pindexPrev.Height - ((pindexPrev.Height + 1) % nPeriod));
            }

            // Walk backwards in steps of nPeriod to find a pindexPrev whose information is known
            List<ChainedBlock> vToCompute = new List<ChainedBlock>();
            while(!this.ContainsKey(pindexPrev?.HashBlock, deployment))
            {
                if(pindexPrev.GetMedianTimePast() < nTimeStart)
                {
                    // Optimization: don't recompute down further, as we know every earlier block will be before the start time
                    this.Set(pindexPrev?.HashBlock, deployment, ThresholdState.Defined);
                    break;
                }
                vToCompute.Add(pindexPrev);
                pindexPrev = pindexPrev.GetAncestor(pindexPrev.Height - nPeriod);
            }
            // At this point, cache[pindexPrev] is known
            this.assert(this.ContainsKey(pindexPrev?.HashBlock, deployment));
            ThresholdState state = this.Get(pindexPrev?.HashBlock, deployment);

            // Now walk forward and compute the state of descendants of pindexPrev
            while(vToCompute.Count != 0)
            {
                ThresholdState stateNext = state;
                pindexPrev = vToCompute[vToCompute.Count - 1];
                vToCompute.RemoveAt(vToCompute.Count - 1);

                switch(state)
                {
                    case ThresholdState.Defined:
                        {
                            if(pindexPrev.GetMedianTimePast() >= nTimeTimeout)
                            {
                                stateNext = ThresholdState.Failed;
                            }
                            else if(pindexPrev.GetMedianTimePast() >= nTimeStart)
                            {
                                stateNext = ThresholdState.Started;
                            }
                            break;
                        }
                    case ThresholdState.Started:
                        {
                            if(pindexPrev.GetMedianTimePast() >= nTimeTimeout)
                            {
                                stateNext = ThresholdState.Failed;
                                break;
                            }
                            // We need to count
                            ChainedBlock pindexCount = pindexPrev;
                            int count = 0;
                            for(int i = 0; i < nPeriod; i++)
                            {
                                if(this.Condition(pindexCount, deployment))
                                {
                                    count++;
                                }
                                pindexCount = pindexCount.Previous;
                            }
                            if(count >= nThreshold)
                            {
                                stateNext = ThresholdState.LockedIn;
                            }
                            break;
                        }
                    case ThresholdState.LockedIn:
                        {
                            // Always progresses into ACTIVE.
                            stateNext = ThresholdState.Active;
                            break;
                        }
                    case ThresholdState.Failed:
                    case ThresholdState.Active:
                        {
                            // Nothing happens, these are terminal states.
                            break;
                        }
                }
                this.Set(pindexPrev?.HashBlock, deployment, state = stateNext);
            }

            return state;
        }

        private ThresholdState Get(uint256 hash, BIP9Deployments deployment)
        {
            if(hash == null)
                return ThresholdState.Defined;
            ThresholdState?[] threshold;
            if(!this.cache.TryGetValue(hash, out threshold))
                throw new InvalidOperationException("Should never happen");
            if(threshold[(int)deployment] == null)
                throw new InvalidOperationException("Should never happen");
            return threshold[(int)deployment].Value;
        }

        private void Set(uint256 hash, BIP9Deployments deployment, ThresholdState state)
        {
            if(hash == null)
                return;
            ThresholdState?[] threshold;
            if(!this.cache.TryGetValue(hash, out threshold))
            {
                threshold = new ThresholdState?[ArraySize];
                this.cache.Add(hash, threshold);
            }
            threshold[(int)deployment] = state;
        }

        private bool ContainsKey(uint256 hash, BIP9Deployments deployment)
        {
            if(hash == null)
                return true;
            ThresholdState?[] threshold;
            if(!this.cache.TryGetValue(hash, out threshold))
                return false;
            return threshold[(int)deployment].HasValue;
        }

        private bool Condition(ChainedBlock pindex, BIP9Deployments deployment)
        {
            return (((pindex.Header.Version & VERSIONBITS_TOP_MASK) == VERSIONBITS_TOP_BITS) && (pindex.Header.Version & this.Mask(deployment)) != 0);
        }

        public uint Mask(BIP9Deployments deployment)
        {
            return ((uint)1) << this._Consensus.BIP9Deployments[deployment].Bit;
        }

        private void assert(bool v)
        {
            if(!v)
                throw new Exception("Assertion failed");
        }
    }
}
