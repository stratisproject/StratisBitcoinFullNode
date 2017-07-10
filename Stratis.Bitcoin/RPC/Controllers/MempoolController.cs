using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.RPC.Controllers
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
