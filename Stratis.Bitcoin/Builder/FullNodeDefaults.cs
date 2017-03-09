using System;
using Microsoft.Extensions.Configuration;


namespace Stratis.Bitcoin {
   public static class FullNodeDefaults {
      public static readonly string ApplicationKey = "applicationName";
      public static readonly string StartupAssemblyKey = "startupAssembly";

      public static readonly string DetailedErrorsKey = "detailedErrors";
      public static readonly string EnvironmentKey = "environment";
      public static readonly string CaptureStartupErrorsKey = "captureStartupErrors";
   }
}