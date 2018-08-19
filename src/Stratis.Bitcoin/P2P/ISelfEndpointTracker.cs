using System;
using System.Collections.Generic;
using System.Net;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Tracker for endpoints known to be self.
    /// </summary>
    public interface ISelfEndpointTracker
    {
        /// <summary>Update external IP address and peer score of the node.</summary>
        /// <paramref name="ipEndPoint">The endpoint to add.</paramref>
        /// <paramref name="suppliedEndPointIsFinal">Whether the ipEndPoint supplied should be marked final on the endpoint tracker.</paramref>
        /// <paramref name="ipEndPointPeerScore">Peer score of the ipEndPoint supplied. Default value of 1.</paramref>
        void UpdateAndAssignMyExternalAddress(IPEndPoint ipEndPoint, bool suppliedEndPointIsFinal, int ipEndPointPeerScore = 1);

        /// <summary>External IP address of the node.</summary>
        IPEndPoint MyExternalAddress { get; }

        /// <summary>Adds an endpoint to the currently known list.</summary>
        /// <param name="ipEndPoint">The endpoint to add.</param>
        void Add(IPEndPoint ipEndPoint);

        /// <summary>Checks if endpoint is known to be itself against the pruned dictionary.</summary>
        /// <param name="ipEndPoint">The endpoint to check.</param>
        /// <returns><c>true</c> if self, <c>false</c> if unknown.</returns>
        bool IsSelf(IPEndPoint ipEndPoint);
    }
}