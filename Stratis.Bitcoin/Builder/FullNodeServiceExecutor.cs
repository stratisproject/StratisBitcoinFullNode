using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin {
   /// <summary>
   /// A builder for <see cref="IFullNode"/>
   /// </summary>
   public class FullNodeServiceExecutor {
      private readonly IEnumerable<IFullNodeService> _services;
      private readonly ILogger<FullNodeServiceExecutor> _logger;

      public FullNodeServiceExecutor(ILogger<FullNodeServiceExecutor> logger, IEnumerable<IFullNodeService> services) {
         _logger = logger;
         _services = services;
      }

      public void Start() {
         try {
            Execute(service => service.Start());
         }
         catch (Exception ex) {
            //todo: log properly
            //_logger.ApplicationError(LoggerEventIds.HostedServiceStartException, "An error occurred starting the application", ex);
         }
      }

      public void Stop() {
         try {
            Execute(service => service.Stop());
         }
         catch (Exception ex) {
            //todo: log properly
            //_logger.ApplicationError(LoggerEventIds.HostedServiceStopException, "An error occurred stopping the application", ex);
         }
      }

      private void Execute(Action<IFullNodeService> callback) {
         List<Exception> exceptions = null;

         foreach (var service in _services) {
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