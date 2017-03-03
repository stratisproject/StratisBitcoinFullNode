using System;
using Stratis.Bitcoin;

namespace Stratis.Dashboard.Infrastructure {
   public class FullNodeGetter : IFullNodeGetter {
      FullNode _fullNode;

      public FullNodeGetter(FullNode fullNode) {
         this._fullNode = fullNode;
      }

      public FullNode GetFullNode() {
         return _fullNode;
      }
   }
}
