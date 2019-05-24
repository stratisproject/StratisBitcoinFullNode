namespace Stratis.FederatedSidechains.AdminDashboard.Entities
{
    public class Peer
    {
        public string Endpoint { get; set; }
        public string Type { get; set; }
        public int Height { get; set; }
        public string Version { get; set; }
    }
}