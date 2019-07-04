using System.Collections.Generic;
using Stratis.FederatedSidechains.AdminDashboard.Entities;

namespace Stratis.FederatedSidechains.AdminDashboard.Models
{
    public class SidechainNodeModel : StratisNodeModel
    {
        public List<PendingPoll> PoAPendingPolls { get; set; }
        public int FedMemberCount { get; set; }
        public string BlockProducerHits { get; set; }

        public decimal BlockProducerHitsValue { get; set; }
    }
}