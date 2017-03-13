using Microsoft.Extensions.DependencyInjection;

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