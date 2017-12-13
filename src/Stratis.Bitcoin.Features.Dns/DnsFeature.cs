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
        private readonly IPeerAddressManager peerAddressManager;

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

        /// <inheritdoc />
        public override void Initialize()
        {
            this.logger.LogInformation("Starting DNS");
            // todo: add implementation.            
        }
    }
}
