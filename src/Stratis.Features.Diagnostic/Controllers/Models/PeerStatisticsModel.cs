using System.Collections.Generic;
using System.Linq;
using Stratis.Features.Diagnostic.PeerDiagnostic;

namespace Stratis.Features.Diagnostic.Controllers.Models
{
    public class PeerStatisticsModel
    {
        public string PeerEndPoint { get; set; }

        public bool Connected { get; set; }

        public bool Inbound { get; set; }

        public long BytesSent { get; set; }

        public long BytesReceived { get; set; }

        public int ReceivedMessages { get; set; }

        public int SentMessages { get; set; }

        public List<string> LatestEvents { get; set; }

        public PeerStatisticsModel(PeerStatistics peer, bool connected)
        {
            this.LatestEvents = new List<string>();
            this.Connected = connected;

            if (peer != null)
            {
                this.PeerEndPoint = peer.PeerEndPoint.ToString();
                this.Inbound = peer.Inbound;
                this.BytesReceived = peer.BytesReceived;
                this.BytesSent = peer.BytesSent;
                this.LatestEvents = peer.LatestEvents.ToList();
                this.ReceivedMessages = peer.ReceivedMessages;
                this.SentMessages = peer.SentMessages;
            }
        }
    }
}
