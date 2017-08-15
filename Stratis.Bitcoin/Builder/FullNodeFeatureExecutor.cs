using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;

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
                this.logger.LogError("An error occurred starting the application", ex);
                throw;
            }
        }

        /// <inheritdoc />
        public void Stop()
        {
            try
            {
                Execute(service => service.Stop());
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
        /// <remarks>This method catches exception of start/stop methods and then, after all start/stop methods were called 
        /// for all features, it throws AggregateException if there were any exceptions.</remarks>
        private void Execute(Action<IFullNodeFeature> callback)
        {
            List<Exception> exceptions = null;

            if (this.node.Services != null)
            {
                foreach (var service in this.node.Services.Features)
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