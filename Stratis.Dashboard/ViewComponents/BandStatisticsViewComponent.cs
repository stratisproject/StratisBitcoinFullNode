using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Stratis.Dashboard.Infrastructure;
using Stratis.Dashboard.Model;

namespace Stratis.Dashboard.ViewComponents {
   public class BandStatisticsViewComponent : ViewComponent {
      private IFullNodeGetter _nodeGetter;

      public BandStatisticsViewComponent(IFullNodeGetter nodeGetter) {
         _nodeGetter = nodeGetter;
      }

      public async Task<IViewComponentResult> InvokeAsync(int? limit) {
         var fullNode = _nodeGetter.GetFullNode();

         var statistics = fullNode.ConnectionManager.ConnectedNodes.Select(n => n.Counter).Aggregate(
            new BandStatistics(),
            (accumulator, it) => {
               accumulator.Received += it.ReadenBytes;
               accumulator.Sent += it.WrittenBytes;
               return accumulator;
            }
         );

         return View(statistics);
      }
   }
}