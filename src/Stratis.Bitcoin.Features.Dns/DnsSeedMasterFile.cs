using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// This class defines a DNS masterfile used to cache the whitelisted peers discovered by the DNS Seed service that supports saving
    /// and loading from a stream.
    /// This is based on 3rd party library https://github.com/kapetan/dns.
    /// </summary>
    public class DnsSeedMasterFile : IMasterFile
    {
        /// <summary>
        /// Sets the default ttl.
        /// </summary>
        private static readonly TimeSpan DEFAULT_TTL = new TimeSpan(0);

        /// <summary>
        /// The default time to live.
        /// </summary>
        private TimeSpan ttl = DEFAULT_TTL;

        /// <summary>
        /// The resource record entries in the master file.
        /// </summary>
        protected IList<IResourceRecord> entries = new List<IResourceRecord>();

        /// <summary>
        /// Initializes a new instance of a <see cref="DnsSeedMasterFile"/> class.
        /// </summary>
        /// <param name="ttl">The time to live.</param>
        public DnsSeedMasterFile(TimeSpan ttl)
        {
            this.ttl = ttl;
        }

        /// <summary>
        /// Initializes a new instance of a <see cref="DnsSeedMasterFile"/> class.
        /// </summary>
        public DnsSeedMasterFile() { }

        public DnsSeedMasterFile(IList<IResourceRecord> resourceRecords)
        {
            this.entries = resourceRecords;
        }

        /// <summary>
        /// Identifies if the domain matches the entry.
        /// </summary>
        /// <param name="domain">The domain to match.</param>
        /// <param name="entry">The entry to match.</param>
        /// <returns><c>True</c> if there is a match, otherwise <c>false</c>.</returns>
        private static bool Matches(Domain domain, Domain entry)
        {
            string[] labels = entry.ToString().Split('.');
            var patterns = new string[labels.Length];

            for (int i = 0; i < labels.Length; i++)
            {
                string label = labels[i];
                patterns[i] = label == "*" ? "(\\w+)" : Regex.Escape(label);
            }

            var re = new Regex("^" + string.Join("\\.", patterns) + "$");
            return re.IsMatch(domain.ToString());
        }

        /// <summary>
        /// Creates the serializer for loading and saving the master file contents.
        /// </summary>
        /// <returns></returns>
        private JsonSerializer CreateSerializer()
        {
            var settings = new Newtonsoft.Json.JsonSerializerSettings();
            settings.Converters.Add(new ResourceRecordConverter());
            settings.Formatting = Formatting.Indented;

            return JsonSerializer.Create(settings);
        }

        /// <summary>
        /// Gets a list of matching <see cref="IResourceRecord"/> objects.
        /// </summary>
        /// <param name="domain">The domain to match on.</param>
        /// <param name="type">The type to match on.</param>
        /// <returns>The matching entries.</returns>
        public IList<IResourceRecord> Get(Domain domain, RecordType type)
        {
            // Fix logic from 3rd party library to support the ANY DNS query record type and
            // when dig uses +trace option, the recursion request to get the NS record doesn't specify a domain
            // which is catered for by testing to see if the domain is empty.
            return this.entries.Where(e => (string.IsNullOrWhiteSpace(domain.ToString()) || Matches(domain, e.Name)) && (e.Type == type || type == RecordType.ANY)).ToList();
        }

        /// <summary>
        /// Gets a list of matching <see cref="IResourceRecord"/> objects.
        /// </summary>
        /// <param name="question">The <see cref="Question"/>used to match on.</param>
        /// <returns>The matching entries.</returns>
        public IList<IResourceRecord> Get(Question question)
        {
            return this.Get(question.Name, question.Type);
        }

        /// <summary>
        /// Loads the saved masterfile from the specified stream.
        /// </summary>
        /// <param name="stream">The stream containing the masterfile.</param>
        public void Load(Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));

            using (var textReader = new JsonTextReader(new StreamReader(stream)))
            {
                JsonSerializer serializer = this.CreateSerializer();
                this.entries = serializer.Deserialize<List<IResourceRecord>>(textReader);
            }
        }

        /// <summary>
        /// Saves the cached masterfile to the specified stream.
        /// </summary>
        /// <param name="stream">The stream to write the masterfile to.</param>
        public void Save(Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));

            var textWriter = new JsonTextWriter(new StreamWriter(stream));
            JsonSerializer serializer = this.CreateSerializer();

            // Send a copy of the entries to the serializer because the collection can be modified during serialization.
            serializer.Serialize(textWriter, this.entries.ToList());
            textWriter.Flush();
        }

        /// <inheritdoc />
        public void Seed(DnsSettings dnsSettings)
        {
            // Check if SOA record exists for host.
            int count = this.Get(new Question(new Domain(dnsSettings.DnsHostName), RecordType.SOA)).Count;
            if (count == 0)
            {
                // Add SOA record for host.
                this.entries.Add(new StartOfAuthorityResourceRecord(new Domain(dnsSettings.DnsHostName), new Domain(dnsSettings.DnsNameServer), new Domain(dnsSettings.DnsMailBox.Replace('@', '.'))));
            }

            // Check if NS record exists for host.
            count = this.Get(new Question(new Domain(dnsSettings.DnsHostName), RecordType.NS)).Count;
            if (count == 0)
            {
                // Add NS record for host.
                this.entries.Add(new NameServerResourceRecord(new Domain(dnsSettings.DnsHostName), new Domain(dnsSettings.DnsNameServer)));
            }
        }
    }
}