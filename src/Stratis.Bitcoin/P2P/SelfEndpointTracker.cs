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

        /// <summary>External IP address of the node.</summary>
        public IPEndPoint MyExternalAddress { get; set; }

        /// <summary>Whether IP address of the node is final or can be updated.</summary>
        public bool IsMyExternalAddressFinal { get; set; } = false;

        /// <summary>Peer score of external IP address of the node.</summary>
        public int MyExternalAddressPeerScore { get; set; }

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
        public void UpdateAndAssignMyExternalAddress(IPEndPoint ipEndPoint, int? ipEndPointScore = null)
        {
            if (this.IsMyExternalAddressFinal)
               return;

            if (ipEndPoint == null)
                return;

            // Explicitly supplied a score for this endpoint.
            if (ipEndPointScore.HasValue)
            {
                this.MyExternalAddress = ipEndPoint;
                this.MyExternalAddressPeerScore = ipEndPointScore.Value;
                return;
            }

            // If it was the same as value that was there we just increment the score by 1.
            if (ipEndPoint.Equals(this.MyExternalAddress))
            {
                this.MyExternalAddressPeerScore += 1;
            }
            else
            {
                // If it was different we decrement the score by 1.
                this.MyExternalAddressPeerScore -= 1;
            }

            // If the new score is 0 we replace the old one with the new one with score 1.
            if (this.MyExternalAddressPeerScore <= 0)
            {
                this.MyExternalAddress = ipEndPoint;
                this.MyExternalAddressPeerScore = 1;
            }
        }
    }
}