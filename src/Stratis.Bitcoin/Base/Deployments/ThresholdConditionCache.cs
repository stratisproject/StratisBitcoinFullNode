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
        public static int VersionbitsLastOldBlockVersion = 4;

        /** What bits to set in version for versionbits blocks */
        public static uint VersionbitsTopBits = 0x20000000;

        /** What bitmask determines whether versionbits is in use */
        public static uint VersionbitsTopMask = 0xE0000000;

        /** Total bits available for versionbits */
        public static int VersionbitsNumBits = 29;

        public static readonly int ArraySize;

        static ThresholdConditionCache()
        {
            ArraySize = Enum.GetValues(typeof(BIP9Deployments)).Length;
        }

        private NBitcoin.Consensus consensus;

        private Dictionary<uint256, ThresholdState?[]> cache = new Dictionary<uint256, ThresholdState?[]>();

        public ThresholdConditionCache(NBitcoin.Consensus consensus)
        {
            Guard.NotNull(consensus, nameof(consensus));

            this.consensus = consensus;
        }

        public ThresholdState[] GetStates(ChainedHeader pindexPrev)
        {
            return Enum.GetValues(typeof(BIP9Deployments))
                .OfType<BIP9Deployments>()
                .Select(b => this.GetState(pindexPrev, b))
                .ToArray();
        }

        public ThresholdState GetState(ChainedHeader indexPrev, BIP9Deployments deployment)
        {
            int period = this.consensus.MinerConfirmationWindow;
            int threshold = this.consensus.RuleChangeActivationThreshold;
            var timeStart = this.consensus.BIP9Deployments[deployment]?.StartTime;
            var timeTimeout = this.consensus.BIP9Deployments[deployment]?.Timeout;

            // Check if this deployment is always active.
            if (timeStart == Utils.UnixTimeToDateTime(BIP9DeploymentsParameters.AlwaysActive))
            {
                return ThresholdState.Active;
            }

            // A block's state is always the same as that of the first of its period, so it is computed based on a pindexPrev whose height equals a multiple of nPeriod - 1.
            if (indexPrev != null)
            {
                indexPrev = indexPrev.GetAncestor(indexPrev.Height - ((indexPrev.Height + 1) % period));
            }

            // Walk backwards in steps of nPeriod to find a pindexPrev whose information is known
            List<ChainedHeader> vToCompute = new List<ChainedHeader>();
            while (!this.ContainsKey(indexPrev?.HashBlock, deployment))
            {
                if (indexPrev.GetMedianTimePast() < timeStart)
                {
                    // Optimization: don't recompute down further, as we know every earlier block will be before the start time
                    this.Set(indexPrev?.HashBlock, deployment, ThresholdState.Defined);
                    break;
                }
                vToCompute.Add(indexPrev);
                indexPrev = indexPrev.GetAncestor(indexPrev.Height - period);
            }
            // At this point, cache[pindexPrev] is known
            this.Assert(this.ContainsKey(indexPrev?.HashBlock, deployment));
            ThresholdState state = this.Get(indexPrev?.HashBlock, deployment);

            // Now walk forward and compute the state of descendants of pindexPrev
            while (vToCompute.Count != 0)
            {
                ThresholdState stateNext = state;
                indexPrev = vToCompute[vToCompute.Count - 1];
                vToCompute.RemoveAt(vToCompute.Count - 1);

                switch (state)
                {
                    case ThresholdState.Defined:
                        {
                            if (indexPrev.GetMedianTimePast() >= timeTimeout)
                            {
                                stateNext = ThresholdState.Failed;
                            }
                            else if (indexPrev.GetMedianTimePast() >= timeStart)
                            {
                                stateNext = ThresholdState.Started;
                            }
                            break;
                        }
                    case ThresholdState.Started:
                        {
                            if (indexPrev.GetMedianTimePast() >= timeTimeout)
                            {
                                stateNext = ThresholdState.Failed;
                                break;
                            }
                            // We need to count
                            ChainedHeader pindexCount = indexPrev;
                            int count = 0;
                            for (int i = 0; i < period; i++)
                            {
                                if (this.Condition(pindexCount, deployment))
                                {
                                    count++;
                                }
                                pindexCount = pindexCount.Previous;
                            }
                            if (count >= threshold)
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
                this.Set(indexPrev?.HashBlock, deployment, state = stateNext);
            }

            return state;
        }

        private ThresholdState Get(uint256 hash, BIP9Deployments deployment)
        {
            if (hash == null)
                return ThresholdState.Defined;
            ThresholdState?[] threshold;
            if (!this.cache.TryGetValue(hash, out threshold))
                throw new InvalidOperationException("Should never happen");
            if (threshold[(int)deployment] == null)
                throw new InvalidOperationException("Should never happen");
            return threshold[(int)deployment].Value;
        }

        private void Set(uint256 hash, BIP9Deployments deployment, ThresholdState state)
        {
            if (hash == null)
                return;
            ThresholdState?[] threshold;
            if (!this.cache.TryGetValue(hash, out threshold))
            {
                threshold = new ThresholdState?[ArraySize];
                this.cache.Add(hash, threshold);
            }
            threshold[(int)deployment] = state;
        }

        private bool ContainsKey(uint256 hash, BIP9Deployments deployment)
        {
            if (hash == null)
                return true;
            ThresholdState?[] threshold;
            if (!this.cache.TryGetValue(hash, out threshold))
                return false;
            return threshold[(int)deployment].HasValue;
        }

        private bool Condition(ChainedHeader pindex, BIP9Deployments deployment)
        {
            return (((pindex.Header.Version & VersionbitsTopMask) == VersionbitsTopBits) && (pindex.Header.Version & this.Mask(deployment)) != 0);
        }

        public uint Mask(BIP9Deployments deployment)
        {
            return ((uint)1) << this.consensus.BIP9Deployments[deployment].Bit;
        }

        private void Assert(bool v)
        {
            if (!v)
                throw new Exception("Assertion failed");
        }
    }
}
