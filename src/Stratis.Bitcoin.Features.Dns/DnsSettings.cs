using System;
using Microsoft.Extensions.Logging;
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
        public static DnsSettings Load(NodeSettings nodeSettings, DnsSettings dnsSettings = null)
        {
            ILogger logger = nodeSettings.LoggerFactory.CreateLogger(typeof(DnsSettings).FullName);

            logger.LogTrace("()");

            dnsSettings = dnsSettings ?? new DnsSettings();

            TextFileConfiguration config = nodeSettings.ConfigReader;
            
            dnsSettings.DnsListenPort = config.GetOrDefault<int>("dnslistenport", DefaultDnsListenPort);
            logger.LogDebug("DNS Seed Service listen port is {0}, if running as DNS Seed.", dnsSettings.DnsListenPort);

            dnsSettings.DnsFullNode = config.GetOrDefault<bool>("dnsfullnode", false);
            if (dnsSettings.DnsFullNode)
                logger.LogDebug("DNS Seed Service is set to run as a full node, if running as DNS Seed.", dnsSettings.DnsListenPort);

            dnsSettings.DnsPeerBlacklistThresholdInSeconds = config.GetOrDefault("dnspeerblacklistthresholdinseconds", DefaultDnsPeerBlacklistThresholdInSeconds);
            logger.LogDebug("DnsPeerBlacklistThresholdInSeconds set to {0}.", dnsSettings.DnsPeerBlacklistThresholdInSeconds);

            dnsSettings.DnsHostName = config.GetOrDefault<string>("dnshostname", null);
            logger.LogDebug("DNS Seed Service host name set to {0}.", dnsSettings.DnsHostName);

            dnsSettings.DnsNameServer = config.GetOrDefault<string>("dnsnameserver", null);
            logger.LogDebug("DNS Seed Service nameserver set to {0}.", dnsSettings.DnsNameServer);

            dnsSettings.DnsMailBox = config.GetOrDefault<string>("dnsmailbox", null);
            logger.LogDebug("DNS Seed Service mailbox set to {0}.", dnsSettings.DnsMailBox);

            dnsSettings.callback?.Invoke(dnsSettings);

            // Verify that the DNS host, nameserver and mailbox arguments are set.
            if (string.IsNullOrWhiteSpace(dnsSettings.DnsHostName) || string.IsNullOrWhiteSpace(dnsSettings.DnsNameServer) || string.IsNullOrWhiteSpace(dnsSettings.DnsMailBox))
                throw new ConfigurationException("When running as a DNS Seed service, the -dnshostname, -dnsnameserver and -dnsmailbox arguments must be specified on the command line.");

            logger.LogTrace("(-)");

            return dnsSettings;
        }
    }
}
