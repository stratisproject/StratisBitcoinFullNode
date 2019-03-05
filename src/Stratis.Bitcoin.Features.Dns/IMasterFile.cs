using System.Collections.Generic;
using System.IO;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// This interface defines a DNS masterfile used to cache the whitelisted peers discovered by the DNS Seed service.
    /// </summary>
    public interface IMasterFile
    {
        /// <summary>
        /// Gets a list of resource records that match the question.
        /// </summary>
        /// <param name="question">The question to ask of the masterfile.</param>
        /// <returns>A list of resource records.</returns>
        IList<IResourceRecord> Get(Question question);

        /// <summary>
        /// Loads the saved masterfile from the specified stream.
        /// </summary>
        /// <param name="stream">The stream containing the masterfile.</param>
        void Load(Stream stream);

        /// <summary>
        /// Saves the cached masterfile to the specified stream.
        /// </summary>
        /// <param name="stream">The stream to write the masterfile to.</param>
        void Save(Stream stream);

        /// <summary>
        /// Seeds the masterfile with the SOA and NS DNS records with the DNS specific settings.
        /// </summary>
        void Seed(DnsSettings dnsSettings);
    }
}