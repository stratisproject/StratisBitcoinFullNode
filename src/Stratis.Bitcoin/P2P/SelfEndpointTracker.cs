using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Tracker for endpoints known to be self.
    /// </summary>
    public class SelfEndpointTracker : ISelfEndpointTracker
    {
        /// <summary>Stored endpoints expire after this many hours and need to be readded.
        /// <para>So that changes in Endpoint Address don't prevent a connection to another node
        /// in the event they have the Endpoint Address that used to belong to this node</para>
        /// </summary>
        public const int ExpiryInHours = 12;

        /// <summary>Provides the datetime in controllable way.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Dictionary to hold the endpoints currently known to be itself and their expiry date.</summary>
        private ConcurrentDictionary<IPEndPoint, DateTime> knownSelfEndpointsAndExpiryDate = new ConcurrentDictionary<IPEndPoint, DateTime>();

        /// <summary>Constructor</summary>
        /// <param name="dateTimeProvider">Datetime provider injected so it can be controlled in tests.</param>
        public SelfEndpointTracker(IDateTimeProvider dateTimeProvider)
        {
            this.dateTimeProvider = dateTimeProvider;
        }

        /// <inheritdoc/>
        public void Add(IPEndPoint ipEndPoint)
        {
            this.PruneExpired();

            DateTime newExpiryDate = this.dateTimeProvider.GetUtcNow().AddHours(ExpiryInHours);
            this.knownSelfEndpointsAndExpiryDate.AddOrUpdate(ipEndPoint, newExpiryDate, (key, value) => newExpiryDate);
        }
        /// <inheritdoc/>
        public bool IsSelf(IPEndPoint ipEndPoint)
        {
            this.PruneExpired();
            return this.knownSelfEndpointsAndExpiryDate.ContainsKey(ipEndPoint);
        }

        /// <summary>Removes the expired items from the dictionary.</summary>
        private void PruneExpired()
        {
            Dictionary<IPEndPoint, DateTime> unexpiredItems = 
                this.knownSelfEndpointsAndExpiryDate
                    .Where(x => x.Value > this.dateTimeProvider.GetUtcNow())
                    .ToDictionary(x => x.Key, x => x.Value);

            this.knownSelfEndpointsAndExpiryDate = new ConcurrentDictionary<IPEndPoint, DateTime>(unexpiredItems);
        }
    }
}