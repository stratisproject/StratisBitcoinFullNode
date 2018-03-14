using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Stratis.Bitcoin.P2P
{
    public class SelfEndpointTracker : ISelfEndpointTracker
    {
        public const int ExpiryInHours = 12;
        public Func<DateTime> Now = () => DateTime.UtcNow;

        private Dictionary<IPEndPoint, DateTime> currentKnownEndpointsAndExpiryDate = new Dictionary<IPEndPoint, DateTime>();

        public void Add(IPEndPoint ipEndPoint)
        {
            this.Prune();

            if (this.currentKnownEndpointsAndExpiryDate.ContainsKey(ipEndPoint))
                this.currentKnownEndpointsAndExpiryDate.Remove(ipEndPoint);

            this.currentKnownEndpointsAndExpiryDate.Add(ipEndPoint, this.Now().AddHours(ExpiryInHours));
        }

        public bool IsSelf(IPEndPoint ipEndPoint)
        {
            this.Prune();
            return this.currentKnownEndpointsAndExpiryDate.ContainsKey(ipEndPoint);
        }

        private void Prune()
        {
            this.currentKnownEndpointsAndExpiryDate = 
                this.currentKnownEndpointsAndExpiryDate
                    .Where(x => x.Value > this.Now())
                    .ToDictionary(x => x.Key, x => x.Value);
        }
    }
}