using System.Net;

namespace NBitcoin
{
    /// <summary>
    /// Represent a DNS seed.
    /// This is intended to help nodes to connect to the network on their first run.
    /// As such, DNS seeds must be run by entities in which some level of trust if given by the community running the nodes.
    /// </summary>
    public class DNSSeedData
    {
        /// <summary> A list of IP addresses associated with this host. </summary>
        private IPAddress[] addresses;

        /// <summary> The name given to this DNS seed. </summary>
        public string Name { get; }

        /// <summary> The DNS server host. </summary>
        public string Host { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DNSSeedData"/> class.
        /// </summary>
        /// <param name="name">The name given to this DNS seed.</param>
        /// <param name="host">The DNS server host.</param>
        public DNSSeedData(string name, string host)
        {
            this.Name = name;
            this.Host = host;
        }

        /// <summary>
        /// Gets the IP addresses of nodes associated with the host.
        /// </summary>
        /// <returns>A list of IP addresses.</returns>
        public IPAddress[] GetAddressNodes()
        {
            if (this.addresses != null)
            {
                return this.addresses;
            }

            this.addresses = Dns.GetHostAddressesAsync(this.Host).GetAwaiter().GetResult();

            return this.addresses;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{this.Name}({this.Host})";
        }
    }
}
