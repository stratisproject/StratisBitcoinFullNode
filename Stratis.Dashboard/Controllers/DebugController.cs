using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Stratis.Dashboard.Model;
using Stratis.Dashboard.Infrastructure;

namespace Stratis.Dashboard.Controllers {
   [Route("/api/debug/[action]")]
   public class DebugController : Controller {
      public IFullNodeGetter FullNodeGetter { get; private set; }

      public DebugController(IFullNodeGetter nodeGetter) {
         this.FullNodeGetter = nodeGetter;
      }


      public IActionResult Mine() {
         var node = FullNodeGetter.GetFullNode();

         List<NBitcoin.uint256> result = null;

         if (node != null) {
            // generate 1 block
            result = node.Miner.GenerateBlocks(
               new Stratis.Bitcoin.Miner.ReserveScript() { reserveSfullNodecript = new NBitcoin.Key().ScriptPubKey },
               1,
               100000000,
               false
            );
         }

         return Json(new {
            result = result
         });
      }
   }
}
