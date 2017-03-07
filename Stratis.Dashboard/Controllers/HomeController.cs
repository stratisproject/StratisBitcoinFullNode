using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Stratis.Dashboard.Model;
using Stratis.Dashboard.Infrastructure;

namespace Stratis.Dashboard.Controllers {
   public class HomeController : Controller {
      public IFullNodeGetter FullNodeGetter { get; private set; }

      public HomeController(IFullNodeGetter nodeGetter) {
         this.FullNodeGetter = nodeGetter;
      }

      public IActionResult Index() {
         var node = FullNodeGetter.GetFullNode();

         if (node.ConnectionManager == null) {
            return View("NodeNotStarted");
         }
         else if (node == null) {
            return View("NullNode");
         }

         var dashboard = new DashBoard() {
            ConnectedPeers = node.ConnectionManager.ConnectedNodes.Where(n => n.IsConnected).Count(),
            CurrentHeight = node.BlockStoreManager.ChainState.HighestValidatedPoW.Height,
            MemPoolTransactionsCount = node.MempoolManager.PerformanceCounter.MempoolSize
         };

         return View("DashBoard", dashboard);
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
