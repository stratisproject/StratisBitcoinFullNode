using System;
using System.Collections.Generic;
using System.Net;
using ConcurrentCollections;
using Microsoft.Extensions.Logging;

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

        /// <summary>
        /// Initializes an instance of the self endpoint tracker.
        /// </summary>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public SelfEndpointTracker(ILoggerFactory loggerFactory)
        {
            this.lockObject = new object();
            this.IsMyExternalAddressFinal = false;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc/>
        public void Add(IPEndPoint ipEndPoint)
        {
            this.knownSelfEndpoints.Add(ipEndPoint);
        }

        /// <inheritdoc/>
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
                if (ipEndPoint.Equals(this.MyExternalAddress))
                {
                    this.MyExternalAddressPeerScore += 1;
                    this.logger.LogTrace("(-)[SUPPLIED_EXISTING]");
                    return;
                }

                // If it was different we decrement the score by 1.
                this.MyExternalAddressPeerScore -= 1;
                this.logger.LogTrace("Different endpoint '{0}' supplied. Score decremented to {1}.", ipEndPoint, this.MyExternalAddressPeerScore);

                // If the new score is 0 we replace the old one with the new one with score 1.
                if (this.MyExternalAddressPeerScore <= 0)
                {
                    this.logger.LogTrace("Score for old endpoint '{0}' is <= 0. Updating endpoint to '{1}' and resetting peer score to 1.", this.MyExternalAddress, ipEndPoint);
                    this.MyExternalAddress = ipEndPoint;
                    this.MyExternalAddressPeerScore = 1;
                }
            }
        }
    }
}