using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Dashboard.Model {
   public class DashBoard {

      public int ConnectedPeers { get; set; }
      public int CurrentHeight { get; set; }

      public long MemPoolTransactionsCount { get; set; }

   }
}
