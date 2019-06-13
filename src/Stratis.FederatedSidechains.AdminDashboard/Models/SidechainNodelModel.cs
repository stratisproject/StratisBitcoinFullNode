using System.Collections.Generic;
using Stratis.FederatedSidechains.AdminDashboard.Entities;

namespace Stratis.FederatedSidechains.AdminDashboard.Models
{
    public class SidechainNodelModel : StratisNodeModel
    {
        public List<PendingPoll> PoAPendingPolls { get; set; }
    }
}