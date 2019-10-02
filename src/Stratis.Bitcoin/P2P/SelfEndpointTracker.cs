using System.Net;
using ConcurrentCollections;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Utilities.Extensions;
using TracerAttributes;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Tracker for endpoints known to be self.
    /// </summary>
    public class SelfEndpointTracker : ISelfEndpointTracker
    {
        /// <summary>Hashset to hold the endpoints currently known to be itself.</summary>
        private readonly ConcurrentHashSet<IPEndPoint> knownSelfEndpoints = new ConcurrentHashSet<IPEndPoint>();

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Peer score of external IP address of the node.</summary>
        internal int MyExternalAddressPeerScore { get; set; }

        /// <summary>Whether IP address of the node is final or can be updated.</summary>
        private bool IsMyExternalAddressFinal { get; set; }

        /// <summary>Protects access to <see cref="MyExternalAddress"/>, <see cref="MyExternalAddressPeerScore"/> and <see cref="IsMyExternalAddressFinal"/>.</summary>
        private readonly object lockObject;

        /// <inheritdoc/>
        public IPEndPoint MyExternalAddress { get; private set; }

        public int MyExternalPort { get; private set; }

        /// <summary>
        /// Initializes an instance of the self endpoint tracker.
        /// </summary>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public SelfEndpointTracker(ILoggerFactory loggerFactory, ConnectionManagerSettings connectionManagerSettings)
        {
            this.lockObject = new object();
            this.IsMyExternalAddressFinal = false;
            this.MyExternalAddress = connectionManagerSettings.ExternalEndpoint;
            this.MyExternalPort = connectionManagerSettings.Port;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc/>
        public void Add(IPEndPoint ipEndPoint)
        {
            this.knownSelfEndpoints.Add(ipEndPoint);
        }

        /// <inheritdoc/>
        [NoTrace]
        public bool IsSelf(IPEndPoint ipEndPoint)
        {
            return this.knownSelfEndpoints.Contains(ipEndPoint);
        }

        /// <inheritdoc/>
        public void UpdateAndAssignMyExternalAddress(IPEndPoint ipEndPoint, bool suppliedEndPointIsFinal, int ipEndPointScore = 0)
        {
            lock (this.lockObject)
            {
                if (suppliedEndPointIsFinal)
                {
                    this.MyExternalAddress = ipEndPoint;
                    this.IsMyExternalAddressFinal = true;
                    this.logger.LogTrace("(-)[SUPPLIED_FINAL]");
                    return;
                }

                if (this.IsMyExternalAddressFinal)
                {
                    this.logger.LogTrace("(-)[EXISTING_FINAL]");
                    return;
                }

                // If it was the same as value that was there we just increment the score by 1.
                // Only the address portion is comparable, the port will be an ephemeral port != our server port if this is an outbound connection. So we replace it later with our configured port for self endpoint tracking.
                if (ipEndPoint.MapToIpv6().Address.Equals(this.MyExternalAddress.MapToIpv6().Address))
                {
                    this.MyExternalAddressPeerScore += 1;
                    this.logger.LogTrace("(-)[SUPPLIED_EXISTING]");
                    return;
                }

                // If it was different we decrement the score by 1.
                this.MyExternalAddressPeerScore -= 1;
                this.logger.LogDebug("Different endpoint '{0}' supplied. Score decremented to {1}.", ipEndPoint, this.MyExternalAddressPeerScore);

                // If the new score is 0 we replace the old one with the new one with score 1.
                if (this.MyExternalAddressPeerScore <= 0)
                {
                    var replacementEndpoint = new IPEndPoint(ipEndPoint.MapToIpv6().Address, this.MyExternalPort);
                    this.logger.LogDebug("Score for old endpoint '{0}' is <= 0. Updating endpoint to '{1}' and resetting peer score to 1.", this.MyExternalAddress, replacementEndpoint);
                    this.MyExternalAddress = replacementEndpoint;
                    this.MyExternalAddressPeerScore = 1;
                }
            }
        }
    }
}