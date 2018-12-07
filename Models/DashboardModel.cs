namespace Stratis.FederatedSidechains.AdminDashboard.Models
{
    public class DashboardModel
    {
        public bool IsCacheBuilt { get; set; } = false;
        public bool Status { get; set; }
        public string MainchainWalletAddress { get; set; }
        public string SidechainWalletAddress { get; set; }
        public string[] MiningPublicKeys { get; set; }
        public StratisNodeModel StratisNode { get; set; }
        public StratisNodeModel SidechainNode { get; set; }
    }
}