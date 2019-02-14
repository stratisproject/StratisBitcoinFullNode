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
    public interface IFullNodeFeatureExecutor : IDisposable
    {
        /// <summary>
        /// Starts all registered features of the associated full node.
        /// </summary>
        void Initialize();
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
        public void Initialize()
        {
            try
            {
                this.Execute(service => service.ValidateDependencies(this.node.Services));
                this.Execute(service => service.InitializeAsync().GetAwaiter().GetResult());
            }
            catch
            {
                this.logger.LogError("An error occurred starting the application.");
                this.logger.LogTrace("(-)[INITIALIZE_EXCEPTION]");
                throw;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            try
            {
                this.Execute(feature => feature.Dispose(), true);
            }
            catch
            {
                this.logger.LogError("An error occurred stopping the application.");
                this.logger.LogTrace("(-)[DISPOSE_EXCEPTION]");
                throw;
            }
        }

        /// <summary>
        /// Executes start or stop method of all the features registered with the associated full node.
        /// </summary>
        /// <param name="callback">Delegate to run start or stop method of the feature.</param>
        /// <param name="disposing">Reverse the order of which the features are executed.</param>
        /// <exception cref="AggregateException">Thrown in case one or more callbacks threw an exception.</exception>
        private void Execute(Action<IFullNodeFeature> callback, bool disposing = false)
        {
            if (this.node.Services == null)
            {
                this.logger.LogTrace("(-)[NO_SERVICES]");
                return;
            }

            List<Exception> exceptions = null;

            if (disposing)
            {
                // When the node is shutting down, we need to dispose all features, so we don't break on exception.
                foreach (IFullNodeFeature feature in this.node.Services.Features.Reverse())
                {
                    try
                    {
                        callback(feature);
                    }
                    catch (Exception exception)
                    {
                        if (exceptions == null)
                            exceptions = new List<Exception>();

                        this.LogAndAddException(exceptions, exception);
                    }
                }
            }
            else
            {
                // When the node is starting we don't continue initialization when an exception occurs.
                try
                {
                    // Initialize features that are flagged to start before the base feature.
                    foreach (IFullNodeFeature feature in this.node.Services.Features.OrderByDescending(f => f.InitializeBeforeBase))
                    {
                        callback(feature);
                    }
                }
                catch (Exception exception)
                {
                    if (exceptions == null)
                        exceptions = new List<Exception>();

                    this.LogAndAddException(exceptions, exception);
                }
            }

            // Throw an aggregate exception if there were any exceptions.
            if (exceptions != null)
            {
                this.logger.LogTrace("(-)[EXECUTION_FAILED]");
                throw new AggregateException(exceptions);
            }
        }

        private void LogAndAddException(List<Exception> exceptions, Exception exception)
        {
            exceptions.Add(exception);

            this.logger.LogError("An error occurred: '{0}'", exception.ToString());
        }
    }
}
