using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Dashboard.Model {
   public class Message {
      public DateTime Date { get; set; }
      public string From { get; set; }
      public string Text { get; set; }
   }
}
