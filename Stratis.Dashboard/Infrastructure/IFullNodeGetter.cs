using Stratis.Bitcoin;

namespace Stratis.Dashboard.Infrastructure {
   public interface IFullNodeGetter {
      FullNode GetFullNode();
   }
}
