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
}
