using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using ConcurrentCollections;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities.Extensions;

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

        /// <summary>External IP address of the node.</summary>
        public IPEndPoint MyExternalAddress { get; set; }

        /// <summary>Whether IP address of the node is final or can be updated.</summary>
        public bool IsMyExternalAddressFinal { get; set; } = false;

        /// <summary>Peer score of external IP address of the node.</summary>
        public int MyExternalAddressPeerScore { get; set; }

        /// <summary>
        /// Initializes an instance of the self endpoint tracker.
        /// </summary>
        public SelfEndpointTracker() : this(new LoggerFactory())
        {
        }

        /// <summary>
        /// Initializes an instance of the self endpoint tracker.
        /// </summary>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public SelfEndpointTracker(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.logger.LogTrace("()");
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
        public void UpdateAndAssignMyExternalAddress(IPEndPoint ipEndPoint, bool suppliedEndpointIsFinal, int ipEndPointScore = 0)
        {
            if (ipEndPoint == null)
            {
                this.logger.LogTrace("(IP ENDPOINT IS NULL)");
                return;
            }

            // override peer score if supplied
            if (ipEndPointScore != 0)
            {
                this.MyExternalAddress = ipEndPoint;
                this.MyExternalAddressPeerScore = ipEndPointScore;
                this.logger.LogTrace("(Peer score is non default for {0}.  Setting to {1}.)", ipEndPoint.ToString(), ipEndPointScore);
                return;
            }

            if (suppliedEndpointIsFinal)
            {
                this.MyExternalAddress = ipEndPoint;
                this.IsMyExternalAddressFinal = true;
                this.logger.LogTrace("(Supplied endpoint {0} is final)", ipEndPoint.ToString());
                return;
            }

            if (this.IsMyExternalAddressFinal)
            {
                this.logger.LogTrace("(Found an existing final endpoint {0})", this.MyExternalAddress.ToString());
                return;
            }

            // If it was the same as value that was there we just increment the score by 1.
            if (ipEndPoint.Equals(this.MyExternalAddress))
            {
                this.MyExternalAddressPeerScore += 1;
                this.logger.LogTrace("(Same endpoint {0} supplied.  Score incremented to {1})", ipEndPoint, this.MyExternalAddressPeerScore);
            }
            else
            {
                // If it was different we decrement the score by 1.
                this.MyExternalAddressPeerScore -= 1;
                this.logger.LogTrace("(Different endpoint {0} supplied.  Score decremented to {1})", ipEndPoint, this.MyExternalAddressPeerScore);
            }

            // If the new score is 0 we replace the old one with the new one with score 1.
            if (this.MyExternalAddressPeerScore <= 0)
            {
                this.logger.LogTrace("(Score for old endpoint {0} is <= 0. Updating endpoint to {1} and resetting peer score to 1)", this.MyExternalAddress, ipEndPoint);
                this.MyExternalAddress = ipEndPoint;
                this.MyExternalAddressPeerScore = 1;
            }
        }
    }
}