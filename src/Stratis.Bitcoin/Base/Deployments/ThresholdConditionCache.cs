using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments.Models;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Base.Deployments
{
    /// <summary>
    /// This class tracks the current activation state of each BIP9 deployment.
    /// </summary>
    public class ThresholdConditionCache
    {
        // What block version to use for new blocks (pre versionbits).
        private const int VersionbitsLastOldBlockVersion = 4;

        // BIP9 reserves the top 3 bits to identify this (001) and future mechanisms (top bits 010 and 011).
        // When a block nVersion does not have top bits 001, it is treated as if all bits are 0 for the purposes of deployments.
        private const uint VersionbitsTopMask = 0xE0000000;

        // Represents bits 001 of the VersionBitsTopMask to indicate that this is a BIP9 version.
        public const uint VersionbitsTopBits = 0x20000000;

        // Total bits available for versionbits.
        private const int VersionbitsNumBits = 29;

        // Array size required to hold all BIP9 deployment activation states.
        public int ArraySize => this.consensus.BIP9Deployments.Length;

        // Used to access the deployments, confirmation window and activation threshold.
        private IConsensus consensus;

        // Cache of BIP9 deployment states keyed by block hash.
        private Dictionary<uint256, ThresholdState?[]> cache = new Dictionary<uint256, ThresholdState?[]>();

        /// <summary>
        /// Constructs this object containing the BIP9 deployment states cache.
        /// </summary>
        /// <param name="consensus">Records the consensus object containing the activation parameters.</param>
        public ThresholdConditionCache(IConsensus consensus)
        {
            Guard.NotNull(consensus, nameof(consensus));

            this.consensus = consensus;
        }

        /// <summary>
        /// Get the states of all BIP 9 deployments listed in the <see cref="BIP9Deployments"/> enumeration.
        /// </summary>
        /// <param name="pindexPrev">The previous header of the block to determine the states for.</param>
        /// <returns>An array of <see cref="ThresholdState"/> objects.</returns>
        public ThresholdState[] GetStates(ChainedHeader pindexPrev)
        {
            ThresholdState[] array = new ThresholdState[this.consensus.BIP9Deployments.Length];

            for (int i = 0; i < array.Length; i++)
            {
                array[i] = this.GetState(pindexPrev, i);
            }

            return array;
        }

        /// <summary>
        /// Computes the metrics of all BIP9 deployments for a given block.
        /// </summary>
        /// <param name="indexPrev">The block at which to compute the metrics.</param>
        /// <param name="thresholdStates">The current state of each BIP9 deployment.</param>
        /// <returns>A <see cref="ThresholdStateModel" /> object containg the metrics.</returns>
        public List<ThresholdStateModel> GetThresholdStateMetrics(ChainedHeader indexPrev, ThresholdState[] thresholdStates)
        {
            var thresholdStateModels = new List<ThresholdStateModel>();
            ThresholdState[] array = new ThresholdState[this.consensus.BIP9Deployments.Length];

            for (int deploymentIndex = 0; deploymentIndex < array.Length; deploymentIndex++)
            {
                if (this.consensus.BIP9Deployments[deploymentIndex] == null) continue;

                DateTime? timeStart = this.consensus.BIP9Deployments[deploymentIndex]?.StartTime.Date;
                DateTime? timeTimeout = this.consensus.BIP9Deployments[deploymentIndex]?.Timeout.Date;
                int threshold = this.consensus.RuleChangeActivationThreshold;

                int votes = 0;
                int currentHeight = indexPrev.Height + 1;
                int period = this.consensus.MinerConfirmationWindow;

                // First ancestor outside last confirmation window.
                ChainedHeader periodStartsHeader = indexPrev.GetAncestor(indexPrev.Height - (currentHeight % period));
                int periodEndsHeight = periodStartsHeader.Height + period;

                while (indexPrev != periodStartsHeader)
                {
                    if (this.Condition(indexPrev, deploymentIndex))
                    {
                        votes++;
                    }

                    indexPrev = indexPrev.Previous;
                }

                thresholdStateModels.Add(new ThresholdStateModel()
                {
                    DeploymentIndex = deploymentIndex,
                    Votes = votes,
                    TimeStart = timeStart,
                    TimeTimeOut = timeTimeout,
                    Threshold = threshold,
                    Height = currentHeight,
                    PeriodStartHeight = periodStartsHeader.Height,
                    PeriodEndHeight = periodEndsHeight,
                    StateValue = thresholdStates[deploymentIndex],
                    ThresholdState = ((ThresholdState) thresholdStates[deploymentIndex]).ToString()
                });
            }

            return thresholdStateModels;
        }

        /// <summary>
        /// Determines the state of a BIP from the cache and/or the chain header history and the corresponding version bits.
        /// </summary>
        /// <param name="indexPrev">The previous header of the chain header to determine the states for.</param>
        /// <param name="deployment">The deployment to check the state of.</param>
        /// <returns>The current state of the deployment.</returns>
        public ThresholdState GetState(ChainedHeader indexPrev, int deployment)
        {
            int period = this.consensus.MinerConfirmationWindow;
            int threshold = this.consensus.RuleChangeActivationThreshold;
            DateTimeOffset? timeStart = this.consensus.BIP9Deployments[deployment]?.StartTime;
            DateTimeOffset? timeTimeout = this.consensus.BIP9Deployments[deployment]?.Timeout;

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

            // Walk backwards in steps of nPeriod to find a pindexPrev whose information is known.
            var vToCompute = new List<ChainedHeader>();
            while (!this.ContainsKey(indexPrev?.HashBlock, deployment))
            {
                if (indexPrev.GetMedianTimePast() < timeStart)
                {
                    // Optimization: don't recompute down further, as we know every earlier block will be before the start time.
                    this.Set(indexPrev?.HashBlock, deployment, ThresholdState.Defined);
                    break;
                }

                vToCompute.Add(indexPrev);
                indexPrev = indexPrev.GetAncestor(indexPrev.Height - period);
            }

            // At this point, cache[pindexPrev] is known.
            this.Assert(this.ContainsKey(indexPrev?.HashBlock, deployment));
            ThresholdState state = this.Get(indexPrev?.HashBlock, deployment);

            // Now walk forward and compute the state of descendants of pindexPrev.
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

                            // Counts the "votes" in the confirmation window to determine
                            // whether the rule change activation threshold has been met.
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

                            // If the threshold has been met then lock in the BIP activation.
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

        /// <summary>
        /// Gets the activation state within a given block of a specific BIP9 deployment.
        /// </summary>
        /// <param name="hash">The block hash to determine the BIP9 activation state for.</param>
        /// <param name="deployment">The deployment for which to determine the activation state.</param>
        /// <returns>The activation state.</returns>
        private ThresholdState Get(uint256 hash, int deployment)
        {
            if (hash == null)
                return ThresholdState.Defined;
            ThresholdState?[] threshold;
            if (!this.cache.TryGetValue(hash, out threshold))
                throw new InvalidOperationException("Should never happen");
            if (threshold[deployment] == null)
                throw new InvalidOperationException("Should never happen");
            return threshold[deployment].Value;
        }

        /// <summary>
        /// Sets the activation state for a given block of a specific BIP9 deployment.
        /// </summary>
        /// <param name="hash">The block hash to set the BIP9 activation state for.</param>
        /// <param name="deployment">The deployment for which to set the activation state.</param>
        /// <param name="state">The activation state to set.</param>
        private void Set(uint256 hash, int deployment, ThresholdState state)
        {
            if (hash == null)
                return;
            ThresholdState?[] threshold;
            if (!this.cache.TryGetValue(hash, out threshold))
            {
                threshold = new ThresholdState?[this.ArraySize];
                this.cache.Add(hash, threshold);
            }

            threshold[deployment] = state;
        }

        /// <summary>
        /// Determines if the activation state is available for a given block hash for a specific deployment.
        /// </summary>
        /// <param name="hash">The block hash to determine the BIP9 activation state for.</param>
        /// <param name="deployment">The deployment for which to determine the activation state.</param>
        /// <returns>Returns <c>true</c> if the state is available and <c>false</c> otherwise.</returns>
        [NoTrace]
        private bool ContainsKey(uint256 hash, int deployment)
        {
            if (hash == null)
                return true;
            ThresholdState?[] threshold;
            if (!this.cache.TryGetValue(hash, out threshold))
                return false;
            return threshold[deployment].HasValue;
        }

        /// <summary>
        /// Inspects the chain header to determine whether the version bit of a deployment is active.
        /// </summary>
        [NoTrace]
        private bool Condition(ChainedHeader pindex, int deployment)
        {
            // This restricts us to at most 30 independent deployments. By restricting the top 3 bits to 001 we get 29 out of those
            // for the purposes of this proposal, and support two future upgrades for different mechanisms (top bits 010 and 011).
            // When a block nVersion does not have top bits 001, it is treated as if all bits are 0 for the purposes of deployments.
            return (((pindex.Header.Version & VersionbitsTopMask) == VersionbitsTopBits) && (pindex.Header.Version & this.Mask(deployment)) != 0);
        }

        /// <summary>
        /// Returns the bit mask of the bit representing a specific deployment within the version bits.
        /// </summary>
        /// <param name="deployment">The BIP9 deployment to return the bit mask for.</param>
        /// <returns>The bit mask of the bit representing the deployment within the version bits.</returns>
        [NoTrace]
        public uint Mask(int deployment)
        {
            return ((uint)1) << this.consensus.BIP9Deployments[deployment].Bit;
        }

        /// <summary>
        /// Throws an 'Assertion failed' exception if the passed argument is <c>false</c>.
        /// </summary>
        /// <param name="v">The passed argument which, if false, raises a 'Assertion Failed' exception.</param>
        private void Assert(bool v)
        {
            if (!v)
                throw new Exception("Assertion failed");
        }
    }
}
