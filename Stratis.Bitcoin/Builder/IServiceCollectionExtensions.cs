using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Hosting.Builder;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.PlatformAbstractions;

namespace Stratis.Bitcoin {
   internal static class IServiceCollectionExtensions {
      public static IServiceCollection Clone(this IServiceCollection serviceCollection) {
         IServiceCollection clone = new ServiceCollection();
         foreach (var service in serviceCollection) {
            clone.Add(service);
         }
         return clone;
      }
   }
}