using System;
using Microsoft.Extensions.Configuration;

namespace Stratis.Bitcoin {
   public class FullNodeOptions {

      public string ApplicationName { get; set; }
      public bool CaptureStartupErrors { get; set; }
      public string ContentRootPath { get; set; }
      public bool DetailedErrors { get; set; }
      public string Environment { get; set; }
      public string StartupAssembly { get; set; }
      public string WebRoot { get; set; }

      public FullNodeOptions() { }

      public FullNodeOptions(IConfiguration configuration) {
         if (configuration == null) {
            throw new ArgumentNullException(nameof(configuration));
         }

         ApplicationName = configuration[FullNodeDefaults.ApplicationKey];
         StartupAssembly = configuration[FullNodeDefaults.StartupAssemblyKey];
         DetailedErrors = ParseBool(configuration, FullNodeDefaults.DetailedErrorsKey);
         CaptureStartupErrors = ParseBool(configuration, FullNodeDefaults.CaptureStartupErrorsKey);
         Environment = configuration[FullNodeDefaults.EnvironmentKey];
      }

      private static bool ParseBool(IConfiguration configuration, string key) {
         return string.Equals("true", configuration[key], StringComparison.OrdinalIgnoreCase)
             || string.Equals("1", configuration[key], StringComparison.OrdinalIgnoreCase);
      }
   }
}