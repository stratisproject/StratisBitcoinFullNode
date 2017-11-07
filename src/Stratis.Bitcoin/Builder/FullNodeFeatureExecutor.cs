using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Builder
{
    /// <summary>
    /// Starts and stops all features registered with a full node.
    /// </summary>
    public interface IFullNodeFeatureExecutor
    {
        /// <summary>
        /// Starts all registered features of the associated full node.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops all registered features of the associated full node.
        /// </summary>
        void Stop();
    }

    /// <summary>
    /// Starts and stops all features registered with a full node.
    /// </summary>
    /// <remarks>Borrowed from ASP.NET.</remarks>
    public class FullNodeFeatureExecutor : IFullNodeFeatureExecutor
    {
        /// <summary>Full node which features are to be managed by this executor.</summary>
        private readonly IFullNode node;

        /// <summary>Object logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Initializes an instance of the object with specific full node and logger factory.
        /// </summary>
        /// <param name="fullNode">Full node which features are to be managed by this executor.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the object.</param>
        public FullNodeFeatureExecutor(IFullNode fullNode, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(fullNode, nameof(fullNode));

            this.node = fullNode;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public void Start()
        {
            try
            {
                this.Execute(service => service.ValidateDependencies(this.node.Services));
                this.Execute(service => service.Start());
            }
            catch (Exception ex)
            {
                this.logger.LogError("An error occurred starting the application: {0}", ex);
                throw;
            }
        }

        /// <inheritdoc />
        public void Stop()
        {
            try
            {
                this.Execute(service => service.Stop(), true);
            }
            catch (Exception ex)
            {
                this.logger.LogError("An error occurred stopping the application", ex);
                throw;
            }
        }

        /// <summary>
        /// Executes start or stop method of all the features registered with the associated full node.
        /// </summary>
        /// <param name="callback">Delegate to run start or stop method of the feature.</param>
        /// <param name="reverseOrder">Reverse the order of which the features are executed.</param>
        /// <remarks>This method catches exception of start/stop methods and then, after all start/stop methods were called 
        /// for all features, it throws AggregateException if there were any exceptions.</remarks>
        private void Execute(Action<IFullNodeFeature> callback, bool reverseOrder = false)
        {
            List<Exception> exceptions = null;

            if (this.node.Services != null)
            {
                var iterator = this.node.Services.Features;

                if (reverseOrder)
                    iterator = iterator.Reverse();

                foreach (var service in iterator)
                {
                    try
                    {
                        callback(service);
                    }
                    catch (Exception ex)
                    {
                        if (exceptions == null)
                        {
                            exceptions = new List<Exception>();
                        }

                        exceptions.Add(ex);
                    }
                }

                // Throw an aggregate exception if there were any exceptions
                if (exceptions != null)
                {
                    throw new AggregateException(exceptions);
                }
            }
        }
    }
}