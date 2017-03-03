using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Stratis.Dashboard.Model;
using Stratis.Dashboard.Infrastructure;

namespace Stratis.Dashboard.Controllers {
   public class DebugController : Controller {
      public IFullNodeGetter FullNodeGetter { get; private set; }

      public DebugController(IFullNodeGetter nodeGetter) {
         this.FullNodeGetter = nodeGetter;
      }

      public void Mine(int? blockNumbers) {
         var node = FullNodeGetter.GetFullNode();

         if (node != null) { 
                  // generate 1 block
         node.Miner.GenerateBlocks(new Stratis.Bitcoin.Miner.ReserveScript() {
                     reserveSfullNodecript = new NBitcoin.Key().ScriptPubKey
                  }, 1, 100000000, false);
         }
      }


      public IActionResult NodeSettings() {
         var nodeSettings = FullNodeGetter.GetFullNode()?.Args;

         return View("NodeSettings", nodeSettings);
      }


      public IActionResult Error() {
         return View();
      }



      /// <summary>
      /// returns top peers
      /// </summary>
      /// <returns></returns>
      public IActionResult TopPeers(int? howMany) {
         return ViewComponent(typeof(ViewComponents.PeerListViewComponent), new {
            limit = howMany
         });
      }

      public IActionResult BandStatistics() {
         return ViewComponent(typeof(ViewComponents.BandStatisticsViewComponent));
      }
   }
}
