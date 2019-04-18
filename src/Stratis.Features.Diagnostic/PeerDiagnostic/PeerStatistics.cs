using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using Stratis.Features.Diagnostic.Utils;

namespace Stratis.Features.Diagnostic.PeerDiagnostic
{
    /// <summary>
    /// Holds peer statistics.
    /// <see cref="LatestEvents"/> is a limited size string rapresentation of latest peer events, it's maximum size is specified in the class constructor maxLoggedEvents
    /// </summary>
    public class PeerStatistics
    {
        public IPEndPoint IPEndPoint { get; set; }

        public bool Inbound { get; set; }

        public long BytesSent { get; set; }

        public long BytesReceived { get; set; }

        public int ReceivedMessages { get; set; }

        public int SentMessages { get; set; }

        /// <summary>
        /// Gets or sets the latest events.
        /// </summary>
        /// <value>
        /// The latest events.
        /// </value>
        public ConcurrentFixedSizeQueue<string> LatestEvents { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PeerStatistics" /> class.
        /// </summary>
        /// <param name="maxLoggedEvents">The maximum number of logged events.</param>
        /// <param name="peerEndPoint">The peer end point.</param>
        public PeerStatistics(int maxLoggedEvents, IPEndPoint peerEndPoint)
        {
            this.IPEndPoint = peerEndPoint;
            this.LatestEvents = new ConcurrentFixedSizeQueue<string>(maxLoggedEvents);
        }

        public void LogEvent(string loggedText)
        {
            this.LatestEvents.Enqueue(loggedText);
        }
    }
}
