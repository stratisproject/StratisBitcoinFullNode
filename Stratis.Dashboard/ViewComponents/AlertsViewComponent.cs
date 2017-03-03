using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Stratis.Dashboard.Model;

namespace Stratis.Dashboard.ViewComponents {
   public class AlertsViewComponent : ViewComponent {
      public async Task<IViewComponentResult> InvokeAsync() {
         var alerts = GetFakeMessages();

         return View(alerts);
      }



      private List<Alert> GetFakeMessages() {
         return new List<Model.Alert>() {
            new Alert() {
               Date =DateTime.Now.AddDays(-1),
               Level = Alert.AlertLevel.Info,
               Text ="Information abc"
            },
            new Alert() {
               Date =DateTime.Now.AddDays(-1).AddHours(2),
               Level = Alert.AlertLevel.Success,
               Text ="Wonderful!"
            },
            new Alert() {
               Date =DateTime.Now.AddDays(-1).AddHours(5),
               Level = Alert.AlertLevel.Warning,
               Text ="Watch out!"
            },
            new Alert() {
               Date =DateTime.Now.AddDays(-1).AddHours(8),
               Level = Alert.AlertLevel.Danger,
               Text ="Dammit..."
            }
         };
      }
   }
}