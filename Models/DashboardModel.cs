using Newtonsoft.Json.Linq;

namespace Stratis.FederatedSidechains.AdminDashboard.Models
{
    public class DashboardModel
    {
        public bool IsCacheBuilt { get; set; }
        public bool Status { get; set; }
        public string MainchainWalletAddress { get; set; }
        public string SidechainWalletAddress { get; set; }
        public JArray MiningPublicKeys { get; set; }
        public StratisNodeModel StratisNode { get; set; }
        public StratisNodeModel SidechainNode { get; set; }
    }
}