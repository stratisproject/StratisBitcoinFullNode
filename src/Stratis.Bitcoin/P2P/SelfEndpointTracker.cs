using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using ConcurrentCollections;
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

        /// <summary>External IP address and peer score of the node.</summary>
        public KeyValuePair<IPEndPoint, int> MyExternalAddress { get; set; }

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

        /// <summary>Updates external IP address and peer score of the node.</summary>
        public void UpdateAndAssignMyExternalAddress(IPEndPoint ipEndPoint, ConnectionManager connectionManager)
        {
            // if external IP address supplied this overrides all.
            if (connectionManager.ConnectionSettings.ExternalIpFromNodeConfiguration != null)
            {
                connectionManager.ConnectionSettings.ExternalEndpoint = connectionManager.ConnectionSettings.ExternalIpFromNodeConfiguration;
                return;
            }

            // If external IP address not supplied take first routeable bind address and set score to 10.
            IPEndPoint nodeServerEndpoint = connectionManager.ConnectionSettings.Listen?.FirstOrDefault(x => x.Endpoint.Address.IsRoutable(false))?.Endpoint;
            if (nodeServerEndpoint != null)
            {
                this.MyExternalAddress = new KeyValuePair<IPEndPoint, int>(nodeServerEndpoint, 10);
                connectionManager.ConnectionSettings.ExternalEndpoint = this.MyExternalAddress.Key;
                return;
            }

            // If none supplied or routableable take from version handshake.
            if (ipEndPoint == null)
                return;

            // If it was the same as value that was there we just increment the score by 1.
            if (ipEndPoint.Equals(this.MyExternalAddress.Key))
            {
                this.MyExternalAddress =
                    new KeyValuePair<IPEndPoint, int>(this.MyExternalAddress.Key, this.MyExternalAddress.Value + 1);
            }

            // If it was different we decrement the score by 1.
            this.MyExternalAddress =
                    new KeyValuePair<IPEndPoint, int>(this.MyExternalAddress.Key, this.MyExternalAddress.Value - 1);

            // If the new score is 0 we replace the old one with the new one with score 1.
            if (this.MyExternalAddress.Value <= 0)
            {
                this.MyExternalAddress = new KeyValuePair<IPEndPoint, int>(ipEndPoint, 1);
            }

            connectionManager.ConnectionSettings.ExternalEndpoint = this.MyExternalAddress.Key;
        }
    }
}