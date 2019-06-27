namespace Stratis.FederatedSidechains.AdminDashboard.Settings
{
    public class DefaultEndpointsSettings
    {
        public string StratisNode { get; set; }
        public string SidechainNode { get; set; }
        public string NodeType { get; set; }
    }

    public class NodeTypes
    {
        public const string TenK = "10K";
        public const string FiftyK = "50K";
    }
}