using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base.Deployments
{
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
        private readonly Dictionary<uint256, ThresholdState?[]> cache = new Dictionary<uint256, ThresholdState?[]>();

        // Cache of EnhancedThresholdState keyed by block hash.
        private readonly Dictionary<uint256, EnhancedThresholdState[]> enhancedCache = new Dictionary<uint256, EnhancedThresholdState[]>();

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

                int votes = 0;
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
                            votes = 0;
                            for (int i = 0; i < period; i++)
                            {
                                if (this.Condition(pindexCount, deployment))
                                {
                                    votes++;
                                }

                                pindexCount = pindexCount.Previous;
                            }

                            // If the threshold has been met then lock in the BIP activation.
                            if (votes >= threshold)
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
                this.SetEnhanced(indexPrev?.HashBlock, deployment, state = stateNext, indexPrev.GetMedianTimePast(), votes);
            }

          return state;
        }

        /// <summary>
        /// Class representing the activation states with the count of blocks in each state.
        /// </summary>
        public class EnrichedActivationStateModel
        {
            public int DeploymentIndex { get; }
            public int blocksDefined { get; }
            public int blocksStarted { get; }
            public int blocksLockedIn { get; }
            public int blocksFailed { get; }
            public int blocksActive { get; }
            public DateTimeOffset? medianTimePast { get; }
            public DateTimeOffset? timeStart { get; }
            public DateTimeOffset? timeTimeOut { get; }
            public int threshold { get; }
            public int votes { get; }
            public ThresholdState StateValue { get; }
            public string ThresholdState { get; }

            public EnrichedActivationStateModel(int deploymentIndex, int blocksDefined, int blocksStarted, int blocksLockedIn,
                int blocksFailed, int blocksActive, DateTimeOffset? medianTimePast, DateTimeOffset? timeStart, DateTimeOffset? timeTimeOut, int threshold, int votes, ThresholdState stateValue, string thresholdState)
            {
                this.DeploymentIndex = deploymentIndex;
                this.blocksDefined = blocksDefined;
                this.blocksStarted = blocksStarted;
                this.blocksLockedIn = blocksLockedIn;
                this.blocksFailed = blocksFailed;
                this.blocksActive = blocksActive;
                this.medianTimePast = medianTimePast;
                this.timeStart = timeStart;
                this.timeTimeOut = timeTimeOut;
                this.votes = votes;
                this.threshold = threshold;
                this.StateValue = stateValue;
                this.ThresholdState = thresholdState;
            }
        }

        /// <summary>
        /// Class representing the activation states with the count of blocks in each state.
        /// </summary>
        public class EnhancedThresholdState
        {
            public ThresholdState? ThresholdState { get; set; }
            public DateTimeOffset TimePast { get; set; }
            public DateTimeOffset? TimeOut { get; set; }
            public DateTimeOffset? TimeStart { get; set; }
            public int Threshold { get; set; }
            public int Votes { get; set; }

            public EnhancedThresholdState(ThresholdState? thresholdState, DateTimeOffset timePast, DateTimeOffset? timeOut, DateTimeOffset? timeStart, int threshold, int votes)
            {
                this.ThresholdState = thresholdState;
                this.TimePast = timePast;
                this.TimeOut = timeOut;
                this.TimeStart = timeStart;
                this.Threshold = threshold;
                this.Votes = votes;
            }

            public EnhancedThresholdState()
            {
            }
        }

        /// <summary>
        /// Enriches the activation states with the count of blocks in each state.
        /// </summary>
        /// <param name="thresholdStates">Array of activation states.</param>
        /// <returns>Activation states enumerated by deployment index with block counts.</returns>
        public object EnrichStatesWithBlockMetrics(ThresholdState[] thresholdStates)
        {
            var enrichedActivationStateMode = new List<EnrichedActivationStateModel>();
            var nonNullDeployments = new List<int>();
            for (int i = 0; i < this.consensus.BIP9Deployments.Length; i++)
            {
                if (this.consensus.BIP9Deployments[i] != null) // Only use network defined deployments.
                    nonNullDeployments.Add(i);
            }

            for (int thresholdStateIndex = 0; thresholdStateIndex < thresholdStates.Length; thresholdStateIndex++)
            {
                if (!nonNullDeployments.Contains(thresholdStateIndex)) continue;
                int definedStateCount = this.enhancedCache.Values.Where(x => x[thresholdStateIndex].ThresholdState != null).Count(x => x[thresholdStateIndex].ThresholdState.Equals(ThresholdState.Defined));
                int startedStateCount = this.enhancedCache.Values.Where(x => x[thresholdStateIndex].ThresholdState != null).Count(x => x[thresholdStateIndex].ThresholdState.Equals(ThresholdState.Started));
                int lockInStateCount = this.enhancedCache.Values.Where(x => x[thresholdStateIndex].ThresholdState != null).Count(x => x[thresholdStateIndex].ThresholdState.Equals(ThresholdState.LockedIn));
                int failedStateCount = this.enhancedCache.Values.Where(x => x[thresholdStateIndex].ThresholdState != null).Count(x => x[thresholdStateIndex].ThresholdState.Equals(ThresholdState.Failed));
                int activeStateCount = this.enhancedCache.Values.Where(x => x[thresholdStateIndex].ThresholdState != null).Count(x => x[thresholdStateIndex].ThresholdState.Equals(ThresholdState.Active));

                int maxVotes = (this.enhancedCache.Values.Count > 0) ? this.enhancedCache.Values.Max(x => x[thresholdStateIndex].Votes) : 0;

                int threshold = this.consensus.RuleChangeActivationThreshold;

                DateTimeOffset? timeStart = this.consensus.BIP9Deployments[thresholdStateIndex]?.StartTime;
                DateTimeOffset? timeTimeout = this.consensus.BIP9Deployments[thresholdStateIndex]?.Timeout;
                DateTimeOffset maxTimePast;

                if (this.enhancedCache.Values.Count > 0)
                {
                    maxTimePast = this.enhancedCache.Values.Max(x => x[thresholdStateIndex].TimePast);
                }

                var row = new EnrichedActivationStateModel(thresholdStateIndex, definedStateCount, startedStateCount, lockInStateCount, failedStateCount, activeStateCount, maxTimePast, timeStart, timeTimeout, threshold, maxVotes, thresholdStates[thresholdStateIndex], ((ThresholdState) thresholdStates[thresholdStateIndex]).ToString());
                enrichedActivationStateMode.Add(row);
            }

            return enrichedActivationStateMode;
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
        /// Sets the activation state for a given block of a specific BIP9 deployment with additional state variables.
        /// </summary>
        /// <param name="hash">The block hash to set the BIP9 activation state for.</param>
        /// <param name="deployment">The deployment for which to set the activation state.</param>
        /// <param name="state">The activation state to set.</param>
        /// <param name="medianTimePast">Median block time over <see cref="MedianTimeSpan"/> window from this entry in the chain.</param>
        /// <param name="votes">Votes for activation state to be set.</param>
        private void SetEnhanced(uint256 hash, int deployment, ThresholdState state, DateTimeOffset medianTimePast, int votes)
        {
            if (hash == null)
                return;
            EnhancedThresholdState[] enhancedThreshold;
            if (!this.enhancedCache.TryGetValue(hash, out enhancedThreshold))
            {
                enhancedThreshold = new EnhancedThresholdState[this.ArraySize];

                for (int i = 0; i < this.ArraySize; i++)
                {
                    enhancedThreshold[i] = new EnhancedThresholdState();
                }

                this.enhancedCache.Add(hash, enhancedThreshold);
            }

            enhancedThreshold[deployment].ThresholdState = state;
            enhancedThreshold[deployment].TimePast = medianTimePast;
            enhancedThreshold[deployment].Votes = votes;
        }

        /// <summary>
        /// Deterines if the activation state is available for a given block hash for a specific deployment.
        /// </summary>
        /// <param name="hash">The block hash to determine the BIP9 activation state for.</param>
        /// <param name="deployment">The deployment for which to determine the activation state.</param>
        /// <returns>Returns <c>true</c> if the state is available and <c>false</c> otherwise.</returns>
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
