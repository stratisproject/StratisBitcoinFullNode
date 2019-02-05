using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Base
{
    /// <summary>
    /// Methods related to network peers time synchronization feature.
    /// </summary>
    public interface ITimeSyncBehaviorState : IDisposable
    {
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

        /// <summary> A value indicating whether the system time is not in sync and needs adjustment. </summary>
        bool IsSystemTimeOutOfSync { get; }
    }

    /// <summary>
    /// State of time synchronization feature that stores collected data samples
    /// and calculates adjustments to system time.
    /// </summary>
    /// <remarks>
    /// Bitcoin introduced so called adjusted time, which is implemented as a time offset added
    /// to node's system time. The offset is result of time syncing feature with network peers that
    /// collects samples from "version" network message from anyone who connects with our node
    /// (in any direction). The median of the collected samples is used as the final time offset
    /// the node uses to calculate the adjusted time.
    /// <para>
    /// The actual source of adjusted time is <see cref="IDateTimeProvider"/>. It is the logic
    /// behind its calculation and collection of samples that resides in this class. This class
    /// modifies the date time provider using its interface every time a new time offset sample
    /// that affects the final offset is collected.
    /// </para>
    /// <para>
    /// Bitcoin allowed up to 70 minutes of time adjustment to be made using this mechanism.
    /// However, Bitcoin also allowed the blocks to be mined with timestamps that are off by up
    /// to 2 hours. This is very unlike Stratis' POS, which uses very narrow windows for block
    /// timestamps. This is why we implemented our mechanism of time syncing with peers
    /// and adjusted time calculation slightly differently.
    /// </para>
    /// <para>
    /// We also collect samples from network "version" messages and calculate time difference
    /// for every peer. We DO distinguish between inbound and outbound connections, however.
    /// We consider inbound connections as less reliable sources of information and we introduce
    /// <see cref="OffsetWeightSecurityConstant"/> to reflect that. We keep outbound time offset
    /// samples separated from inbound samples. Our final offset is also a median of collected
    /// samples, but outbound samples have much greater weight in the median calculation
    /// as per the given weight, which is dynamically adjusted depending on the inbound outbound ratio
    /// in order to protect us from all inbound and an accepted percentage of outbound.
    /// </para>
    /// <para>
    /// Bitcoin's implementation only allows certain number of samples to be collected
    /// and once the limit is reached, no more samples are allowed. We do not replicate this
    /// behavior and we implement circular array to store the time offset samples.
    /// This means that once the limit is reached, we replace oldest samples with the new ones.
    /// </para>
    /// <para>
    /// Finally, as the POS chain is much more sensitive to correct time settings, our user
    /// alerting mechanism is triggered much earlier (for much lower time difference) than
    /// the one in Bitcoin.
    /// </para>
    /// </remarks>
    public class TimeSyncBehaviorState : ITimeSyncBehaviorState
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
        public const int MaxInboundSamples = 200;

        /// <summary>Maximal number of samples to keep inside <see cref="outboundTimestampOffsets"/>.</summary>
        public const int MaxOutboundSamples = 200;

        /// <summary>
        /// The value of 3 provides enough security to be protected against up to 33.3% of outbound samples being malicious and all inbound being malicious.
        /// </summary>
        public const int OffsetWeightSecurityConstant = 3;

        /// <summary>Maximal value for <see cref="timeOffset"/> in seconds that does not trigger warnings to user.</summary>
        public const int TimeOffsetWarningThresholdSeconds = 5 * 60;

        /// <summary>
        /// Maximal value for <see cref="timeOffset"/>. If the newly calculated value is over this limit,
        /// the time syncing feature will be switched off.
        /// </summary>
        public const int MaxTimeOffsetSeconds = 25 * 60;

        /// <summary>
        /// Minimal amount of outbound samples that should be collected before time adjustment <see cref="timeOffset"/> is changed.
        /// </summary>
        public const int MinOutboundSampleCount = 4;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>The network the node is running on.</summary>
        private readonly Network network;

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

        /// <summary><c>true</c> if the time sync with peers has been switched off, <c>false</c> otherwise.</summary>
        public bool SwitchedOff { get; private set; }

        /// <summary>
        /// <c>true</c> if the reason for switching the time sync feature off was that <see cref="timeOffset"/>
        /// went over the maximal allowed value, <c>false</c> otherwise.
        /// </summary>
        public bool SwitchedOffLimitReached { get; private set; }

        /// <summary>Periodically shows a console warning to inform the user that the system time needs adjustment,
        /// otherwise the node may not perform correctly on the network.</summary>
        private IAsyncLoop warningLoop;

        /// <inheritdoc/>
        public bool IsSystemTimeOutOfSync { get; private set; }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="nodeLifetime">Global application life cycle control - triggers when application shuts down.</param>
        /// <param name="asyncLoopFactory">Factory for creating background async loop tasks.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        /// <param name="network">The network the node is running on.</param>
        public TimeSyncBehaviorState(IDateTimeProvider dateTimeProvider, INodeLifetime nodeLifetime, IAsyncLoopFactory asyncLoopFactory, ILoggerFactory loggerFactory, Network network)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.dateTimeProvider = dateTimeProvider;
            this.nodeLifetime = nodeLifetime;
            this.asyncLoopFactory = asyncLoopFactory;
            this.network = network;

            this.inboundTimestampOffsets = new CircularArray<TimestampOffsetSample>(MaxInboundSamples);
            this.outboundTimestampOffsets = new CircularArray<TimestampOffsetSample>(MaxOutboundSamples);
            this.inboundSampleSources = new HashSet<IPAddress>();
            this.outboundSampleSources = new HashSet<IPAddress>();

            this.timeOffset = TimeSpan.Zero;
        }

        /// <inheritdoc />
        public bool AddTimeData(IPAddress peerAddress, TimeSpan offsetSample, bool isInboundConnection)
        {
            bool res = false;

            bool startWarningLoopNow = false;
            lock (this.lockObject)
            {
                if (!this.SwitchedOff)
                {
                    HashSet<IPAddress> sources = isInboundConnection ? this.inboundSampleSources : this.outboundSampleSources;
                    bool alreadyIncluded = sources.Contains(peerAddress);
                    if (!alreadyIncluded)
                    {
                        sources.Add(peerAddress);

                        CircularArray<TimestampOffsetSample> samples = isInboundConnection ? this.inboundTimestampOffsets : this.outboundTimestampOffsets;

                        var newSample = new TimestampOffsetSample
                        {
                            Source = peerAddress,
                            TimeOffset = offsetSample
                        };

                        TimestampOffsetSample oldSample;
                        if (samples.Add(newSample, out oldSample))
                        {
                            // If we reached the maximum number of samples, we need to remove oldest sample.
                            sources.Remove(oldSample.Source);
                            this.logger.LogTrace("Oldest sample {0} from peer '{1}' removed.", oldSample.TimeOffset, oldSample.Source);
                        }

                        this.RecalculateTimeOffsetLocked();

                        // If SwitchedOffLimitReached is set, timeOffset is set to zero,
                        // so we need to check both conditions here.
                        if (!this.IsSystemTimeOutOfSync
                            && ((Math.Abs(this.timeOffset.TotalSeconds) > TimeOffsetWarningThresholdSeconds) || this.SwitchedOffLimitReached))
                        {
                            startWarningLoopNow = true;
                            this.IsSystemTimeOutOfSync = true;
                        }

                        res = true;
                    }
                    else this.logger.LogTrace("Sample from peer '{0}' is already included.", peerAddress);
                }
                else this.logger.LogTrace("Time sync feature is switched off.");
            }

            if (startWarningLoopNow)
                this.StartWarningLoop();

            return res;
        }

        /// <summary>
        /// Calculates a new value for <see cref="timeOffset"/> based on existing samples.
        /// </summary>
        /// <remarks>
        /// The caller of this method is responsible for holding <see cref="lockObject"/>.
        /// <para>
        /// The function takes a single copy of each inbound sample and combines them with a dynamic number of
        /// copies of the outbound samples in order to maintain the <see cref="OffsetWeightSecurityConstant"/>.
        /// </para>
        /// <para>
        /// When there are many more inbound samples than outbound, which could be the case
        /// in a malicious attack, the security is still maintained by using a dynamic inbound/outbound
        /// ratio multiplier ratio on the outbound samples that maintains the accepted level of security.
        /// </para>
        /// <para>
        /// We require to have at least <see cref="MinOutboundSampleCount"/> outbound samples to change the value of <see cref="timeOffset"/>.
        /// </para>
        /// </remarks>
        private void RecalculateTimeOffsetLocked()
        {
            if (this.outboundTimestampOffsets.Count >= MinOutboundSampleCount)
            {
                this.logger.LogTrace("We have {0} outbound samples and {1} inbound samples.", this.outboundTimestampOffsets.Count, this.inboundSampleSources.Count);
                List<double> inboundOffsets = this.inboundTimestampOffsets.Select(s => s.TimeOffset.TotalSeconds).ToList();
                List<double> outboundOffsets = this.outboundTimestampOffsets.Select(s => s.TimeOffset.TotalSeconds).ToList();

                double currentInboundToOutboundRatio = this.inboundTimestampOffsets.Count / (double)this.outboundTimestampOffsets.Count;
                int numberOfOutboundCopiesToAdd = (int)Math.Ceiling(currentInboundToOutboundRatio * OffsetWeightSecurityConstant);

                // If there are no inbound, use one of each outbound.
                if (numberOfOutboundCopiesToAdd == 0)
                    numberOfOutboundCopiesToAdd = 1;

                var allSamples = new List<double>();

                for (int i = 0; i < numberOfOutboundCopiesToAdd; i++)
                    allSamples.AddRange(outboundOffsets);

                allSamples.AddRange(inboundOffsets);

                double median = allSamples.Median();
                if (Math.Abs(median) < this.network.MaxTimeOffsetSeconds)
                {
                    this.timeOffset = TimeSpan.FromSeconds(median);
                    this.dateTimeProvider.SetAdjustedTimeOffset(this.timeOffset);
                }
                else
                {
                    this.SwitchedOff = true;
                    this.SwitchedOffLimitReached = true;
                    this.dateTimeProvider.SetAdjustedTimeOffset(TimeSpan.Zero);
                }
            }
            else this.logger.LogTrace("We have {0} outbound samples, which is below required minimum of {1} outbound samples.", this.outboundTimestampOffsets.Count, MinOutboundSampleCount);
        }

        /// <summary>
        /// Starts a loop that warns user via console message about problems with system time settings.
        /// </summary>
        private void StartWarningLoop()
        {
            this.warningLoop = this.asyncLoopFactory.Run($"{nameof(TimeSyncBehavior)}.WarningLoop", token =>
            {
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
                        this.logger.LogCritical(Environment.NewLine
                            + "============================== W A R N I N G ! ==============================" + Environment.NewLine
                            + "Your system time is very different from the time of other network nodes." + Environment.NewLine
                            + "It differs from the network median time by {0} seconds." + Environment.NewLine
                            + "To prevent problems, adjust your system time or check the -synctime command line argument," + Environment.NewLine
                            + "and restart the node." + Environment.NewLine
                            + "=============================================================================" + Environment.NewLine,
                              timeOffsetSeconds);
                    }
                }
                else
                {
                    this.logger.LogCritical(Environment.NewLine
                        + "============================== W A R N I N G ! ==============================" + Environment.NewLine
                        + "Your system time is very different from the time of other network nodes." + Environment.NewLine
                        + "Your time difference to the network median time is over the allowed maximum of {0} seconds." + Environment.NewLine
                        + "The time syncing feature has been switched off as it is no longer considered safe." + Environment.NewLine
                        + "It is likely that you will now reject new blocks or be unable to mine new blocks." + Environment.NewLine
                        + "You need to adjust your system time or check the -synctime command line argument," + Environment.NewLine
                        + "and restart the node." + Environment.NewLine
                        + "=============================================================================" + Environment.NewLine,
                        this.network.MaxTimeOffsetSeconds);
                }

                return Task.CompletedTask;
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpan.FromSeconds(57.3), // Weird number to prevent collisions with some other periodic console outputs.
            startAfter: TimeSpans.FiveSeconds);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.warningLoop?.Dispose();
        }
    }

    /// <summary>
    /// Node behavior that collects time offset samples from network "version" messages
    /// from each peer.
    /// </summary>
    public class TimeSyncBehavior : NetworkPeerBehavior
    {
        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Shared state among time sync behaviors that holds list of obtained samples.</summary>
        private readonly ITimeSyncBehaviorState state;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="state">Shared state among time sync behaviors.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public TimeSyncBehavior(ITimeSyncBehaviorState state, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.dateTimeProvider = dateTimeProvider;
            this.state = state;
        }

        /// <inheritdoc />
        [NoTrace]
        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
        }

        /// <inheritdoc />
        [NoTrace]
        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        /// <inheritdoc />
        [NoTrace]
        public override object Clone()
        {
            var res = new TimeSyncBehavior(this.state, this.dateTimeProvider, this.loggerFactory);
            return res;
        }

        /// <summary>
        /// Event handler that is called when the node receives a network message from the attached peer.
        /// </summary>
        /// <param name="peer">Peer that sent us the message.</param>
        /// <param name="message">Received message.</param>
        /// <remarks>
        /// This handler only cares about "verack" messages, which are only sent once per node
        /// and at the time they are sent the time offset information is parsed by underlaying logic.
        /// <para>
        /// Note that it is not possible to use "version" message here as <see cref="INetworkPeer"/>
        /// does not deliver this message for inbound peers to node behaviors.
        /// </para>
        /// </remarks>
        private Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (message.Message.Payload is VerAckPayload verack)
            {
                IPAddress address = peer.RemoteSocketAddress;
                if (address != null)
                {
                    VersionPayload version = peer.PeerVersion;
                    if (version != null)
                    {
                        TimeSpan timeOffset = version.Timestamp - this.dateTimeProvider.GetTimeOffset();
                        if (timeOffset != null) this.state.AddTimeData(address, timeOffset, peer.Inbound);
                    }
                    else this.logger.LogTrace("Node '{0}' does not have an initialized time offset.", peer.RemoteSocketEndpoint);
                }
                else this.logger.LogTrace("Message received from unknown node's address.");
            }

            return Task.CompletedTask;
        }
    }
}