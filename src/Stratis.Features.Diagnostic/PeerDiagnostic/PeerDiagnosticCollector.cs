using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
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
    internal sealed class PeerDiagnosticCollector : IDisposable
    {
        private object lockPeerStatisticCreation;

        private readonly IAsyncProvider asyncProvider;
        private readonly ISignals signals;
        private readonly INodeLifetime nodeLifetime;
        private readonly NodeSettings nodeSettings;

        private readonly Dictionary<IPEndPoint, PeerStatistics> peersStatistics;

        /// <summary>Maximun mumber of logged events per <see cref="PeerStatistics"/>.</summary>
        private readonly int maxPeerLoggedEvents;

        /// <summary>Non blocking queue that consume received peer events to generate peer statistics.</summary>
        private IAsyncDelegateDequeuer<Event.PeerEventBase> peersEventsQueue;

        /// <summary>Holds a list of event subscriptions.</summary>
        private readonly List<SubscriptionToken> eventSubscriptions;

        public PeerDiagnosticCollector(IAsyncProvider asyncProvider, ISignals signals, INodeLifetime nodeLifetime, NodeSettings nodeSettings)
        {
            this.asyncProvider = asyncProvider;
            this.signals = Guard.NotNull(signals, nameof(signals));
            this.nodeLifetime = nodeLifetime;
            this.nodeSettings = Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.lockPeerStatisticCreation = new object();
            this.eventSubscriptions = new List<SubscriptionToken>();
            this.peersStatistics = new Dictionary<IPEndPoint, PeerStatistics>();

            this.maxPeerLoggedEvents = 10;

            this.peersEventsQueue = this.asyncProvider.CreateAndRunAsyncDelegateDequeuer<Event.PeerEventBase>(nameof(this.peersEventsQueue), UpdatePeerStatistics);
        }

        public void Initialize()
        {
            this.eventSubscriptions.Add(this.signals.Subscribe<Event.PeerConnected>(this.EnqueuePeerEvent));
            this.eventSubscriptions.Add(this.signals.Subscribe<Event.PeerConnectionAttempt>(this.EnqueuePeerEvent));
            this.eventSubscriptions.Add(this.signals.Subscribe<Event.PeerConnectionAttemptFailed>(this.EnqueuePeerEvent));
            this.eventSubscriptions.Add(this.signals.Subscribe<Event.PeerDisconnected>(this.EnqueuePeerEvent));

            this.eventSubscriptions.Add(this.signals.Subscribe<Event.PeerMessageReceived>(this.EnqueuePeerEvent));
            this.eventSubscriptions.Add(this.signals.Subscribe<Event.PeerMessageSent>(this.EnqueuePeerEvent));
            this.eventSubscriptions.Add(this.signals.Subscribe<Event.PeerMessageSendFailure>(this.EnqueuePeerEvent));
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
                    statistics.LogEvent($"Connection attemp FAILED. Reason: {@event.Reason}.");
                    break;
                case Event.PeerDisconnected @event:
                    statistics.Inbound = @event.Inbound;
                    statistics.LogEvent($"Disconnected. Reason: {@event.Reason}. Exception: {@event.Exception?.ToString()}");
                    break;
                case Event.PeerMessageReceived @event:
                    statistics.BytesReceived += @event.Message.MessageSize;
                    statistics.LogEvent($"Message Received: {@event.Message.Payload.Command}");
                    break;
                case Event.PeerMessageSent @event:
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
                lock (this.lockPeerStatisticCreation)
                {
                    // ensures no other threads have created already an entry between existance check and lock acquisition.
                    if (!this.peersStatistics.TryGetValue(peerEndPoint, out statistics))
                    {
                        statistics = new PeerStatistics(this.maxPeerLoggedEvents, peerEndPoint);
                        this.peersStatistics.Add(peerEndPoint, statistics);
                    }
                }
            }

            return statistics;
        }


        public void Dispose()
        {
            foreach (SubscriptionToken subscription in this.eventSubscriptions)
            {
                this.signals.Unsubscribe(subscription);
            }
            this.peersEventsQueue.Dispose();
        }
    }
}
