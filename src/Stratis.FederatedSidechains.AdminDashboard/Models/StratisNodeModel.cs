using System.Collections.Generic;

namespace Stratis.FederatedSidechains.AdminDashboard.Models
{
    public class StratisNodeModel
    {
        public int SyncingStatus { get; set; }
        public string WebAPIUrl { get; set; } = "http://localhost:38221/api";
        public string SwaggerUrl { get; set; } = "http://localhost:38221/swagger";
        public int BlockHeight { get; set; }
        public int MempoolSize { get; set; }
        public string BlockHash { get; set; }
        public double ConfirmedBalance { get; set; }
        public double UnconfirmedBalance { get; set; }
        public List<Peer> Peers { get; set; }
        public List<Peer> FederationMembers { get; set; }
        public object History { get; set; }
        public string CoinTicker { get; set; }
    }
}