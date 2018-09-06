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
        void Initialize();

        /// <summary>
        /// Stops all registered features of the associated full node.
        /// </summary>
        void Dispose();
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
            this.Execute(service => service.ValidateDependencies(this.node.Services));
            this.Execute(service => service.Initialize());
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Execute(feature => feature.Dispose(), true);
        }

        /// <summary>
        /// Executes start or stop method of all the features registered with the associated full node.
        /// </summary>
        /// <param name="callback">Delegate to run start or stop method of the feature.</param>
        /// <param name="reverseOrder">Reverse the order of which the features are executed.</param>
        /// <remarks>This method catches exception of start/stop methods and then, after all start/stop methods were called for all features.</remarks>
        private void Execute(Action<IFullNodeFeature> callback, bool reverseOrder = false)
        {
            this.logger.LogTrace("({0}:{1})", nameof(reverseOrder), reverseOrder);

            if (this.node.Services != null)
            {
                IEnumerable<IFullNodeFeature> iterator = this.node.Services.Features;

                if (reverseOrder)
                    iterator = iterator.Reverse();

                foreach (IFullNodeFeature service in iterator)
                {
                    try
                    {
                        callback(service);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError("Exception occurred: {0}", ex.ToString());
                        throw;
                    }
                }
            }

            this.logger.LogTrace("(-)");
        }
    }
}
