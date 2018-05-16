using System;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;

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

        /// <summary>The callback used to override/constrain/extend the settings provided by the Load method.</summary>
        private Action<DnsSettings> callback = null;

        /// <summary>
        /// Constructs this object.
        /// </summary>
        public DnsSettings()
        {
        }

        /// <summary>
        /// Constructs this object whilst providing a callback to override/constrain/extend 
        /// the settings provided by the Load method.
        /// </summary>
        /// <param name="callback">The callback used to override/constrain/extend the settings provided by the Load method.</param>
        public DnsSettings(Action<DnsSettings> callback)
            : this()
        {
            this.callback = callback;
        }

        /// <summary>
        /// Loads the DNS related settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        /// <param name="dnsSettings">Existing DnsSettings object to add loaded values to.</param>
        public DnsSettings Load(NodeSettings nodeSettings)
        {
            ILogger logger = nodeSettings.LoggerFactory.CreateLogger(typeof(DnsSettings).FullName);

            logger.LogTrace("()");

            TextFileConfiguration config = nodeSettings.ConfigReader;
            
            this.DnsListenPort = config.GetOrDefault<int>("dnslistenport", DefaultDnsListenPort);
            logger.LogDebug("DNS Seed Service listen port is {0}, if running as DNS Seed.", this.DnsListenPort);

            this.DnsFullNode = config.GetOrDefault<bool>("dnsfullnode", false);
            if (this.DnsFullNode)
                logger.LogDebug("DNS Seed Service is set to run as a full node, if running as DNS Seed.", this.DnsListenPort);

            this.DnsPeerBlacklistThresholdInSeconds = config.GetOrDefault("dnspeerblacklistthresholdinseconds", DefaultDnsPeerBlacklistThresholdInSeconds);
            logger.LogDebug("DnsPeerBlacklistThresholdInSeconds set to {0}.", this.DnsPeerBlacklistThresholdInSeconds);

            this.DnsHostName = config.GetOrDefault<string>("dnshostname", null);
            logger.LogDebug("DNS Seed Service host name set to {0}.", this.DnsHostName);

            this.DnsNameServer = config.GetOrDefault<string>("dnsnameserver", null);
            logger.LogDebug("DNS Seed Service nameserver set to {0}.", this.DnsNameServer);

            this.DnsMailBox = config.GetOrDefault<string>("dnsmailbox", null);
            logger.LogDebug("DNS Seed Service mailbox set to {0}.", this.DnsMailBox);

            this.callback?.Invoke(this);

            logger.LogTrace("(-)");

            return this;
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

            NodeSettings.Default().Logger.LogInformation(builder.ToString());
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
