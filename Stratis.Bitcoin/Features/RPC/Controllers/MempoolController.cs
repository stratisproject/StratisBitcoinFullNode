using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.RPC.Controllers
{
    public class MempoolController : BaseRPCController
    {
        public MempoolController(MempoolManager mempoolManager) : base(mempoolManager: mempoolManager)
        {
            Guard.NotNull(this.MempoolManager, nameof(this.MempoolManager));
        }

        [ActionName("getrawmempool")]
        public Task<List<uint256>> GetRawMempool()
        {
            return this.MempoolManager.GetMempoolAsync();
        }
    }
}
