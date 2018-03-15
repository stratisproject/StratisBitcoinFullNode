using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P
{
    public class SelfEndpointTracker : ISelfEndpointTracker
    {
        public const int ExpiryInHours = 12;

        private readonly IDateTimeProvider dateTimeProvider;

        public SelfEndpointTracker(IDateTimeProvider dateTimeProvider)
        {
            this.dateTimeProvider = dateTimeProvider;
        }

        private ConcurrentDictionary<IPEndPoint, DateTime> currentKnownEndpointsAndExpiryDate = new ConcurrentDictionary<IPEndPoint, DateTime>();

        public void Add(IPEndPoint ipEndPoint)
        {
            this.Prune();

            DateTime newExpiryDate = this.dateTimeProvider.GetUtcNow().AddHours(ExpiryInHours);
            this.currentKnownEndpointsAndExpiryDate.AddOrUpdate(ipEndPoint, newExpiryDate, (key, value) => newExpiryDate);
        }

        public bool IsSelf(IPEndPoint ipEndPoint)
        {
            this.Prune();
            return this.currentKnownEndpointsAndExpiryDate.ContainsKey(ipEndPoint);
        }

        private void Prune()
        {
            Dictionary<IPEndPoint, DateTime> dictionary = 
                this.currentKnownEndpointsAndExpiryDate
                    .Where(x => x.Value > this.dateTimeProvider.GetUtcNow())
                    .ToDictionary(x => x.Key, x => x.Value);

            this.currentKnownEndpointsAndExpiryDate = new ConcurrentDictionary<IPEndPoint, DateTime>(dictionary);
        }
    }
}