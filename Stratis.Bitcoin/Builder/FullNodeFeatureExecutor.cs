using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin {
   /// <summary>
   /// A builder for <see cref="IFullNode"/>
   /// </summary>
   public class FullNodeFeatureExecutor {
      private readonly IEnumerable<IFullNodeFeature> _features;

      public FullNodeFeatureExecutor(IEnumerable<IFullNodeFeature> features) {
         _features = features;
      }

      public void Start(FullNode nodeInstance) {
         try {
            Execute(service => service.Start(nodeInstance));
         }
         catch (Exception ex) {
            Logging.Logs.FullNode.LogError("An error occured starting the application");
         }
      }

      public void Stop(FullNode nodeInstance) {
         try {
            Execute(service => service.Stop(nodeInstance));
         }
         catch (Exception ex) {
            Logging.Logs.FullNode.LogError("An error occurred stopping the application");
         }
      }

      private void Execute(Action<IFullNodeFeature> callback) {
         List<Exception> exceptions = null;

         foreach (var service in _features) {
            try {
               callback(service);
            }
            catch (Exception ex) {
               if (exceptions == null) {
                  exceptions = new List<Exception>();
               }

               exceptions.Add(ex);
            }
         }

         // Throw an aggregate exception if there were any exceptions
         if (exceptions != null) {
            throw new AggregateException(exceptions);
         }
      }
   }
}