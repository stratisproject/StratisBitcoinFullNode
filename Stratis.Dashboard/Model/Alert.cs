using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Dashboard.Model {
   public class Alert {
      public enum AlertLevel {
         Info,
         Success,
         Warning,
         Danger
      }

      public DateTime Date { get; set; }
      public AlertLevel Level { get; set; }
      public string Text { get; set; }
   }
}
