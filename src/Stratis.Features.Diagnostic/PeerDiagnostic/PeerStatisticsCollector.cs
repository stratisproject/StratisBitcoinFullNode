using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.Diagnostic.Controllers;
using Event = Stratis.Bitcoin.EventBus.CoreEvents;

namespace Stratis.Features.Diagnostic.PeerDiagnostic
{
    /// <summary>
    /// Subscribe to peer events and keep track of their activities.
    /// A summary of peer activities can be obtained using <see cref="DiagnosticController"/> actions
    /// </summary>
    public sealed class PeerStatisticsCollector : IDisposable
    {
        private object lockStartStopCollecting;

        private readonly IAsyncProvider asyncProvider;
        private readonly ISignals signals;
        private readonly INodeLifetime nodeLifetime;
        private readonly DiagnosticSettings diagnosticSettings;

        /// <summary>Track current collecting status, when true Peer Collector is collecting statistics.</summary>
        public bool Enabled { get; private set; }

        private readonly Dictionary<IPEndPoint, PeerStatistics> peersStatistics;

        /// <summary>Non blocking queue that consume received peer events to generate peer statistics.</summary>
        private IAsyncDelegateDequeuer<Event.PeerEventBase> peersEventsQueue;

        /// <summary>Holds a list of event subscriptions.</summary>
        private readonly List<SubscriptionToken> eventSubscriptions;

        public PeerStatisticsCollector(IAsyncProvider asyncProvider, ISignals signals, DiagnosticSettings diagnosticSettings, INodeLifetime nodeLifetime)
        {
            this.asyncProvider = asyncProvider;
            this.signals = Guard.NotNull(signals, nameof(signals));
            this.nodeLifetime = nodeLifetime;
            this.diagnosticSettings = Guard.NotNull(diagnosticSettings, nameof(diagnosticSettings));

            this.eventSubscriptions = new List<SubscriptionToken>();
            this.peersStatistics = new Dictionary<IPEndPoint, PeerStatistics>();

            this.lockStartStopCollecting = new object();
        }

        public void Initialize()
        {
            this.Enabled = this.diagnosticSettings.PeersStatisticsCollectorEnabled;
            if (this.Enabled)
                StartCollecting();
        }

        private void EnqueuePeerEvent(Event.PeerEventBase @event)
        {
            this.peersEventsQueue.Enqueue(@event);
        }

        private Task UpdatePeerStatistics(Event.PeerEventBase peerEvent, CancellationToken cancellation)
        {
            PeerStatistics statistics = GetPeerStatistics(peerEvent.PeerEndPoint);
            switch (peerEvent)
            {
                case Event.PeerConnected @event:
                    statistics.Inbound = @event.Inbound;
                    statistics.LogEvent($"Peer Connected");
                    break;
                case Event.PeerConnectionAttempt @event:
                    statistics.Inbound = @event.Inbound;
                    statistics.LogEvent($"Attempting Connection");
                    break;
                case Event.PeerConnectionAttemptFailed @event:
                    statistics.Inbound = @event.Inbound;
                    statistics.LogEvent($"Connection attempt FAILED. Reason: {@event.Reason}.");
                    break;
                case Event.PeerDisconnected @event:
                    statistics.Inbound = @event.Inbound;
                    statistics.LogEvent($"Disconnected. Reason: {@event.Reason}. Exception: {@event.Exception?.ToString()}");
                    break;
                case Event.PeerMessageReceived @event:
                    statistics.ReceivedMessages++;
                    statistics.BytesReceived += @event.MessageSize;
                    statistics.LogEvent($"Message Received: {@event.Message.Payload.Command}");
                    break;
                case Event.PeerMessageSent @event:
                    statistics.SentMessages++;
                    statistics.BytesSent += @event.Size;
                    statistics.LogEvent($"Message Sent: {@event.Message.Payload.Command}");
                    break;
                case Event.PeerMessageSendFailure @event:
                    statistics.LogEvent($"Message Send Failure: {@event.Message?.Payload.Command}. Exception: {@event.Exception?.ToString()}");
                    break;
            }

            return Task.CompletedTask;
        }

        private PeerStatistics GetPeerStatistics(IPEndPoint peerEndPoint)
        {
            if (!this.peersStatistics.TryGetValue(peerEndPoint, out PeerStatistics statistics))
            {
                // ensures no other threads have created already an entry between existence check and lock acquisition.
                if (!this.peersStatistics.TryGetValue(peerEndPoint, out statistics))
                {
                    statistics = new PeerStatistics(this.diagnosticSettings.MaxPeerLoggedEvents, peerEndPoint);
                    this.peersStatistics.Add(peerEndPoint, statistics);
                }
            }

            return statistics;
        }


        public void StartCollecting()
        {
            lock (this.lockStartStopCollecting)
            {
                this.peersStatistics.Clear();

                this.peersEventsQueue = this.asyncProvider.CreateAndRunAsyncDelegateDequeuer<Event.PeerEventBase>(nameof(this.peersEventsQueue), UpdatePeerStatistics);

                this.eventSubscriptions.Add(this.signals.Subscribe<Event.PeerConnected>(this.EnqueuePeerEvent));
                this.eventSubscriptions.Add(this.signals.Subscribe<Event.PeerConnectionAttempt>(this.EnqueuePeerEvent));
                this.eventSubscriptions.Add(this.signals.Subscribe<Event.PeerConnectionAttemptFailed>(this.EnqueuePeerEvent));
                this.eventSubscriptions.Add(this.signals.Subscribe<Event.PeerDisconnected>(this.EnqueuePeerEvent));

                this.eventSubscriptions.Add(this.signals.Subscribe<Event.PeerMessageReceived>(this.EnqueuePeerEvent));
                this.eventSubscriptions.Add(this.signals.Subscribe<Event.PeerMessageSent>(this.EnqueuePeerEvent));
                this.eventSubscriptions.Add(this.signals.Subscribe<Event.PeerMessageSendFailure>(this.EnqueuePeerEvent));

                this.Enabled = true;
            }
        }

        public void StopCollecting()
        {
            lock (this.lockStartStopCollecting)
            {
                //unsubscribe from eventbus
                foreach (SubscriptionToken subscription in this.eventSubscriptions)
                {
                    this.signals.Unsubscribe(subscription);
                }

                this.eventSubscriptions.Clear();
                this.peersEventsQueue?.Dispose();

                this.Enabled = false;
            }
        }

        internal List<PeerStatistics> GetStatistics()
        {
            return this.peersStatistics.Values.ToList();
        }

        public void Dispose()
        {
            this.StopCollecting();
        }
    }
}
