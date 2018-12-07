using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.FederatedSidechains.AdminDashboard.Rest
{
    public class ApiResponse
    {
        public bool IsSuccess { get; set; }
        public dynamic Content { get; set; }
    }
}
