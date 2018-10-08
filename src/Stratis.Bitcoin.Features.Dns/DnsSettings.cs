using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// Configuration related to the DNS feature.
    /// </summary>
    public class DnsSettings
    {
        /// <summary>The default value for the DNS listen port.</summary>
        public const int DefaultDnsListenPort = 53;

        /// <summary>The default value which a peer should have last have been connected before being blacklisted in DNS nodes.</summary>
        public const int DefaultDnsPeerBlacklistThresholdInSeconds = 1800;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>The value which a peer should have last have been connected before being blacklisted from the DNS nodes.</summary>
        public int DnsPeerBlacklistThresholdInSeconds { get; set; }

        /// <summary>Defines the host name for the node when running as a DNS Seed service.</summary>
        public string DnsHostName { get; set; }

        /// <summary><c>true</c> if the DNS Seed service should also run as a full node, otherwise <c>false</c>.</summary>
        public bool DnsFullNode { get; set; }

        /// <summary>Defines the port that the DNS server will listen on, by default this is 53.</summary>
        public int DnsListenPort { get; set; }

        /// <summary>Defines the nameserver host name used as the authoritative domain for the DNS seed service.</summary>
        public string DnsNameServer { get; set; }

        /// <summary>Defines the e-mail address used as the administrative point of contact for the domain.</summary>
        public string DnsMailBox { get; set; }

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        public DnsSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(DnsSettings).FullName);            

            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.DnsListenPort = config.GetOrDefault<int>("dnslistenport", DefaultDnsListenPort, this.logger);
            this.DnsFullNode = config.GetOrDefault<bool>("dnsfullnode", false, this.logger);
            this.DnsPeerBlacklistThresholdInSeconds = config.GetOrDefault("dnspeerblacklistthresholdinseconds", DefaultDnsPeerBlacklistThresholdInSeconds, this.logger);
            this.DnsHostName = config.GetOrDefault<string>("dnshostname", null, this.logger);
            this.DnsNameServer = config.GetOrDefault<string>("dnsnameserver", null, this.logger);
            this.DnsMailBox = config.GetOrDefault<string>("dnsmailbox", null, this.logger);
        }

        /// <summary>Prints the help information on how to configure the DNS settings to the logger.</summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"-dnslistenport=<0-65535>  The DNS listen port. Defaults to {DefaultDnsListenPort}.");
            builder.AppendLine($"-dnsfullnode=<0 or 1>     Enables running the DNS Seed service as a full node.");
            builder.AppendLine($"-dnspeerblacklistthresholdinseconds=<seconds>  The number of seconds since a peer last connected before being blacklisted from the DNS nodes. Defaults to {DefaultDnsPeerBlacklistThresholdInSeconds}.");
            builder.AppendLine($"-dnshostname=<string>     The host name for the node when running as a DNS Seed service.");
            builder.AppendLine($"-dnsnameserver=<string>   The DNS Seed Service nameserver.");
            builder.AppendLine($"-dnsmailbox=<string>      The e-mail address used as the administrative point of contact for the domain.");

            NodeSettings.Default(network).Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####DNS Settings####");
            builder.AppendLine($"#The DNS listen port. Defaults to {DefaultDnsListenPort}");
            builder.AppendLine($"#dnslistenport={DefaultDnsListenPort}");
            builder.AppendLine($"#Enables running the DNS Seed service as a full node.");
            builder.AppendLine($"#dnsfullnode=0");
            builder.AppendLine($"#The number of seconds since a peer last connected before being blacklisted from the DNS nodes. Defaults to {DefaultDnsPeerBlacklistThresholdInSeconds}.");
            builder.AppendLine($"#dnspeerblacklistthresholdinseconds={DefaultDnsPeerBlacklistThresholdInSeconds}");
            builder.AppendLine($"#The host name for the node when running as a DNS Seed service.");
            builder.AppendLine($"#dnshostname=<string>");
            builder.AppendLine($"#The DNS Seed Service nameserver.");
            builder.AppendLine($"#dnsnameserver=<string>");
            builder.AppendLine($"#The e-mail address used as the administrative point of contact for the domain.");
            builder.AppendLine($"#dnsmailbox=<string>");
        }
    }
}
