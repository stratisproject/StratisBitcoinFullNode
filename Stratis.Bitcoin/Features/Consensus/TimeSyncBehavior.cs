﻿using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// Methods related to network peers time synchronization feature.
    /// </summary>
    public interface ITimeSyncBehaviorState
    {
        /// <summary>
        /// Calculates current adjusted UTC time.
        /// <para>
        /// Using timestamp samples collected from other network peers 
        /// this method produces the current adjusted UTC timestamp
        /// that is most likely to be accepted as a correct time on the network.
        /// </para>
        /// </summary>
        /// <returns>Adjusted UTC timestamp.</returns>
        DateTime GetAdjustedTime();

        /// <summary>
        /// Adds a time offset sample to the internal database of samples.
        /// <para></para>
        /// </summary>
        /// <param name="peerAddress">IP address of the peer that the sample relates to.</param>
        /// <param name="offsetSample">Difference in the peer's time and our system time.</param>
        /// <param name="isInboundConnection"><c>true</c> if the sample comes from a peer that connected to our node, 
        /// <c>false</c> if the sample comes from a peer that our node connected to.</param>
        /// <returns><c>true</c> if the sample was added to the mix, <c>false</c> otherwise.</returns>
        bool AddTimeData(IPAddress peerAddress, TimeSpan offsetSample, bool isInboundConnection);
    }

    /// <inheritdoc />
    public class TimeSyncBehaviorState : ITimeSyncBehaviorState, IDisposable
    {
        /// <summary>
        /// Description of a single timestamp offset sample from a peer.
        /// </summary>
        public struct TimestampOffsetSample
        {
            /// <summary>Difference of the peer's time to our system time.</summary>
            public TimeSpan TimeOffset { get; set; }

            /// <summary>IP address of the peer that provided this sample.</summary>
            public IPAddress Source { get; set; }
        }

        /// <summary>Maximal number of samples to keep inside <see cref="inboundTimestampOffsets"/>.</summary>
        private const int MaxInboundSamples = 200;

        /// <summary>Maximal number of samples to keep inside <see cref="outboundTimestampOffsets"/>.</summary>
        private const int MaxOutboundSamples = 200;

        /// <summary>Ratio of weights of outbound timestamp offsets and inbound timestamp offsets. 
        /// Ratio of N means that a single outbound sample has equal weight as N inbound samples.</summary>
        private const int OutboundToInboundWeightRatio = 10;

        /// <summary>Maximal value for <see cref="timeOffset"/> in seconds that does not trigger warnings to user.</summary>
        private const int TimeOffsetWarningThresholdSeconds = 5 * 60;

        /// <summary>
        /// Maximal value for <see cref="timeOffset"/>. If the newly calculated value is over this limit, 
        /// the time syncing feature will be switched off.
        /// </summary>
        private const int MaxTimeOffsetSeconds = 25 * 60;

        /// <summary>
        /// Number of unweighted samples required before <see cref="timeOffset"/> is changed. 
        /// <para>
        /// Each inbound sample counts as 1 unweighted sample.
        /// Each outbound sample counts as <see cref="OutboundToInboundWeightRatio"/> unweighted samples.
        /// </para>
        /// </summary>
        private const int MinUnweightedSamplesCount = 40;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>Lock object to protect access to <see cref="timeOffset"/>, <see cref="inboundTimestampOffsets"/>, <see cref="outboundTimestampOffsets"/>,
        /// <see cref="inboundSampleSources"/>, <see cref="outboundSampleSources"/>.</summary>
        private readonly object lockObject = new object();

        /// <summary>Time difference that the behavior adds to the system time to form adjusted time.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private TimeSpan timeOffset;

        /// <summary>List of timestamp offset samples from peers that connected to our node.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly CircularArray<TimestampOffsetSample> inboundTimestampOffsets;

        /// <summary>List of timestamp offset samples from peers that our node connected to.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly CircularArray<TimestampOffsetSample> outboundTimestampOffsets;

        /// <summary>List of IP addresses of peers that provided samples in <see cref="inboundSampleSources"/>.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly HashSet<IPAddress> inboundSampleSources;

        /// <summary>List of IP addresses of peers that provided samples in <see cref="outboundTimestampOffsets"/>.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly HashSet<IPAddress> outboundSampleSources;

        /// <summary><c>true</c> if the warning loop has been started, <c>false</c> otherwise.</summary>
        public bool WarningLoopStarted { get; private set; }

        /// <summary><c>true</c> if the time sync with peers has been switched off, <c>false</c> otherwise.</summary>
        public bool SwitchedOff { get; private set; }

        /// <summary>
        /// <c>true</c> if the reason for switching the time sync feature off was that <see cref="timeOffset"/> 
        /// went over the maximal allowed value, <c>false</c> otherwise.
        /// </summary>
        public bool SwitchedOffLimitReached { get; private set; }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public TimeSyncBehaviorState(IDateTimeProvider dateTimeProvider, INodeLifetime nodeLifetime, IAsyncLoopFactory asyncLoopFactory, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
            this.dateTimeProvider = dateTimeProvider;
            this.nodeLifetime = nodeLifetime;
            this.asyncLoopFactory = asyncLoopFactory;

            this.inboundTimestampOffsets = new CircularArray<TimestampOffsetSample>(MaxInboundSamples);
            this.outboundTimestampOffsets = new CircularArray<TimestampOffsetSample>(MaxOutboundSamples);
            this.inboundSampleSources = new HashSet<IPAddress>();
            this.outboundSampleSources = new HashSet<IPAddress>();

            this.timeOffset = TimeSpan.FromSeconds(0);
            this.WarningLoopStarted = false;
            this.SwitchedOff = false;
            this.SwitchedOffLimitReached = false;
        }

        /// <inheritdoc />
        public DateTime GetAdjustedTime()
        {
            DateTime res = this.dateTimeProvider.GetUtcNow();

            lock (this.lockObject)
            {
                if (!this.SwitchedOff)
                  res = res.Add(this.timeOffset);
            }

            return res;
        }

        /// <inheritdoc />
        public bool AddTimeData(IPAddress peerAddress, TimeSpan offsetSample, bool isInboundConnection)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3},{4}:{5})", nameof(peerAddress), peerAddress, nameof(offsetSample), offsetSample.TotalSeconds, nameof(isInboundConnection), isInboundConnection);

            bool res = false;

            lock (this.lockObject)
            {
                if (!this.SwitchedOff)
                {
                    HashSet<IPAddress> sources = isInboundConnection ? this.inboundSampleSources : this.outboundSampleSources;
                    bool alreadyIncluded = sources.Contains(peerAddress);
                    if (!alreadyIncluded)
                    {
                        CircularArray<TimestampOffsetSample> samples = isInboundConnection ? this.inboundTimestampOffsets : this.outboundTimestampOffsets;

                        TimestampOffsetSample newSample = new TimestampOffsetSample();
                        newSample.Source = peerAddress;
                        newSample.TimeOffset = offsetSample;

                        TimestampOffsetSample oldSample;
                        if (samples.Add(newSample, out oldSample))
                        {
                            // If we reached the maximum number of samples, we need to remove oldest sample.
                            sources.Remove(oldSample.Source);
                            this.logger.LogTrace("Oldest sample {0} from peer '{1}' removed.", oldSample.TimeOffset, oldSample.Source);
                        }

                        RecalculateTimeOffsetLocked();

                        if (!this.WarningLoopStarted && (this.timeOffset.TotalSeconds > TimeOffsetWarningThresholdSeconds))
                        {
                            StartWarningLoop();
                            this.WarningLoopStarted = true;
                        }
                    }
                    else this.logger.LogTrace("Sample from peer '{0}' is already included.", peerAddress);
                }
                else this.logger.LogTrace("Time sync feature is switched off.");
            }

            this.logger.LogTrace("(-):{0}", res);
            return res;
        }

        /// <summary>
        /// Calculates a new value for <see cref="timeOffset"/> based on existing samples.
        /// </summary>
        /// <remarks>
        /// The caller of this method is responsible for holding <see cref="lockObject"/>.
        /// <para>
        /// The function takes a single copy of each inbound sample and combines them with <see cref="OutboundToInboundWeightRatio"/>
        /// copies of outbound samples.
        /// </para>
        /// <para>
        /// We require to have at least <see cref="MinUnweigtedSamplesCount"/> unweighted samples to change the value of <see cref="timeOffset"/>.
        /// </para>
        /// </remarks>
        public void RecalculateTimeOffsetLocked()
        {
            this.logger.LogTrace("()");

            // All samples are formed of one copy of each inbound sample.
            // And N copies of each outbound sample.
            int unweightedSamplesCount = this.inboundTimestampOffsets.Count + OutboundToInboundWeightRatio * this.outboundTimestampOffsets.Count;

            if (unweightedSamplesCount >= MinUnweightedSamplesCount)
            {
                List<double> allSamples = this.inboundTimestampOffsets.Select(s => s.TimeOffset.TotalMilliseconds).ToList();

                List<double> outboundOffsets = this.outboundTimestampOffsets.Select(s => s.TimeOffset.TotalMilliseconds).ToList();
                for (int i = 0; i < OutboundToInboundWeightRatio; i++)
                    allSamples.AddRange(outboundOffsets);

                double median = outboundOffsets.Median();
                if (median < MaxTimeOffsetSeconds)
                {
                    this.timeOffset = TimeSpan.FromSeconds(median);
                }
                else
                {
                    this.timeOffset = TimeSpan.FromSeconds(0);
                    this.SwitchedOff = true;
                    this.SwitchedOffLimitReached = true;
                }
            }
            else this.logger.LogTrace("We have {0} unweighted samples, which is below required minimum of {1} samples.", unweightedSamplesCount, MinUnweightedSamplesCount);

            this.logger.LogTrace("(-):{0}={1}", nameof(this.timeOffset), this.timeOffset.TotalSeconds);
        }

        /// <summary>
        /// Starts a loop that warns user via console message about problems with system time settings.
        /// </summary>
        private void StartWarningLoop()
        {
            this.logger.LogTrace("()");

            this.asyncLoopFactory.Run($"{nameof(TimeSyncBehavior)}.WarningLoop", token =>
            {
                this.logger.LogTrace("()");

                if (!this.SwitchedOffLimitReached)
                {
                    bool timeOffsetWrong = false;
                    double timeOffsetSeconds = 0;
                    lock (this.lockObject)
                    {
                        timeOffsetSeconds = this.timeOffset.TotalSeconds;
                        timeOffsetWrong = timeOffsetSeconds > TimeOffsetWarningThresholdSeconds;
                    }

                    if (timeOffsetWrong)
                    {
                        this.logger.LogCritical(
                              "============================== W A R N I N G ! ==============================\n"
                            + "Your system time is very different than the time of other network nodes.\n"
                            + "To prevent problems, adjust your system time, or check -synctime command line argument.\n"
                            + "Your time difference to the network median time is {0} seconds.\n"
                            + "=============================================================================",
                              timeOffsetSeconds);
                    }
                }
                else
                {
                    this.logger.LogCritical(
                          "============================== W A R N I N G ! ==============================\n"
                        + "Your system time is VERY different than the time of other network nodes.\n"
                        + "Your time difference to the network median time is over the allowed maximum of {0} seconds."
                        + "The time syncing feature has been as it is no longer considered safe.\n"
                        + "It is likely that you will now reject new blocks or be unable to mine new block.\n"
                        + "You need to adjust your system time or check -synctime command line argument."
                        + "=============================================================================",
                          MaxTimeOffsetSeconds);
                }

                this.logger.LogTrace("(-)");
                return Task.CompletedTask;
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpan.FromSeconds(57.3), // Weird number to prevent collisions with some other periodic console outputs.
            startAfter: TimeSpans.FiveSeconds);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            // TODO: Dispose async loop.

            this.logger.LogTrace("(-)");
        }
    }

    /// <summary>
    /// Node behavior that collects time offset samples from network "version" messages
    /// from each peer.
    /// </summary>
    public class TimeSyncBehavior : NodeBehavior
    {
        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Shared state among time sync behaviors that holds list of obtained samples.</summary>
        private readonly TimeSyncBehaviorState state;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="state">Shared state among time sync behaviors.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public TimeSyncBehavior(TimeSyncBehaviorState state, ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
            this.state = state;
        }

        /// <inheritdoc />
        protected override void AttachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedNode.MessageReceived += this.AttachedNode_MessageReceived;

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedNode.MessageReceived -= this.AttachedNode_MessageReceived;

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public override object Clone()
        {
            this.logger.LogTrace("()");

            var res = new TimeSyncBehavior(this.state, this.loggerFactory);

            this.logger.LogTrace("(-)");
            return res;
        }

        /// <summary>
        /// Event handler that is called when the attached node receives a network message.
        /// <para>
        /// This handler only cares about "version" messages, which are only sent once per node 
        /// and contains time information, which is parsed by underlaying logic when the handler 
        /// is called.
        /// </para>
        /// </summary>
        /// <param name="node">Node that received the message.</param>
        /// <param name="message">Received message.</param>
        private void AttachedNode_MessageReceived(Node node, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(node), node.RemoteSocketEndpoint, nameof(message), message.Message.Command);

            VersionPayload version = message.Message.Payload as VersionPayload;
            if (version != null)
            {
                IPAddress address = node.RemoteSocketAddress;
                if (address != null)
                {
                    TimeSpan? timeOffset = node.TimeOffset;
                    if (timeOffset != null) this.state.AddTimeData(address, timeOffset.Value, node.Inbound);
                    else this.logger.LogTrace("Node '{0}' does not have an initialized time offset.", node.RemoteSocketEndpoint);
                }
                else this.logger.LogTrace("Message received from unknown node's address.");
            }

            this.logger.LogTrace("(-)");
        }
    }
}
