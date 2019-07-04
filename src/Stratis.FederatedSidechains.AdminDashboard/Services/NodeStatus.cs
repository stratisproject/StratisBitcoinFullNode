using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.FederatedSidechains.AdminDashboard.Services
{
    public class NodeStatus
    {
        public float SyncingProgress => ConsensusHeight > 0 ? (BlockStoreHeight / ConsensusHeight) * 100 : 0; 
        public float BlockStoreHeight { get; set; } = 0;
        public float ConsensusHeight { get; set; } = 0;
        public string Uptime { get; set; } = String.Empty;
        public string State { get; set; } = "Not Operational";
    }

    public class NodeDashboardStats
    {
        public int HeaderHeight { get; set; } = 0;
        public string AsyncLoops { get; set; } = String.Empty;
        public int AddressIndexerHeight { get; set; } = 0;
        public string OrphanSize { get; set; } = String.Empty;
        public int MissCount { get; set; } = 0;
        public bool IsMining { get; set; } = false;
        public int LastMinedIndex { get; set; } = 0;
        public string BlockProducerHits { get; set; } = string.Empty;
        public decimal BlockProducerHitsValue { get; set; }
    }
}
