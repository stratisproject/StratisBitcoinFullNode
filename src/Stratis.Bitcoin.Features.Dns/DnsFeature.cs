using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// Responsible for managing the Dns feature.
    /// </summary>
    public class DnsFeature : FullNodeFeature
    {
        /// <summary>
        /// Defines the peer address manager.
        /// </summary>
        private readonly IPeerAddressManager peerAddressManager;

        /// <summary>
        /// Defines the logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsFeature"/> class.
        /// </summary>
        /// <param name="peerAddressManager">The peer address manager.</param>
        /// <param name="loggerFactory">The factory to create the logger.</param>
        public DnsFeature(IPeerAddressManager peerAddressManager, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(peerAddressManager, nameof(peerAddressManager));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.peerAddressManager = peerAddressManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Initializes the Dns feature.
        /// </summary>
        public override void Initialize()
        {
            this.logger.LogInformation("Starting DNS");
            // TODO: add implementation.            
        }
    }
}
