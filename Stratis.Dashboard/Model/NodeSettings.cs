using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Dashboard.Model {
   public class NodeSettings {
      public NodeArgs NodeArgs { get; set; }

      public string ConfigurationFileContent { get; set; }
   }
}
