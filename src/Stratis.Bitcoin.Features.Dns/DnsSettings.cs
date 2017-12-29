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
        /// Constructs this object from the NodeSettings and the provided callback.
        /// </summary>
        /// <param name="nodeSettings">The NodeSettings object.</param>
        /// <param name="callback">The callback used to override the node settings.</param>
        public DnsSettings(NodeSettings nodeSettings, Action<DnsSettings> callback = null)
            : this(callback)
        {
            this.Load(nodeSettings);
        }

        /// <summary>
        /// Loads the DNS related settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        public void Load(NodeSettings nodeSettings)
        {
            TextFileConfiguration config = nodeSettings.ConfigReader;
            
            this.DnsListenPort = config.GetOrDefault<int>("dnslistenport", 53);
            nodeSettings.Logger.LogDebug("DNS Seed Service listen port is {0}, if running as DNS Seed.", this.DnsListenPort);

            this.DnsFullNode = config.GetOrDefault<bool>("dnsfullnode", false);
            if (this.DnsFullNode)
                nodeSettings.Logger.LogDebug("DNS Seed Service is set to run as a full node, if running as DNS Seed.", this.DnsListenPort);

            this.DnsPeerBlacklistThresholdInSeconds = config.GetOrDefault("dnspeerblacklistthresholdinseconds", DefaultDnsPeerBlacklistThresholdInSeconds);
            nodeSettings.Logger.LogDebug("DnsPeerBlacklistThresholdInSeconds set to {0}.", this.DnsPeerBlacklistThresholdInSeconds);

            this.DnsHostName = config.GetOrDefault<string>("dnshostname", null);
            nodeSettings.Logger.LogDebug("DNS Seed Service host name set to {0}.", this.DnsHostName);

            this.DnsNameServer = config.GetOrDefault<string>("dnsnameserver", null);
            nodeSettings.Logger.LogDebug("DNS Seed Service nameserver set to {0}.", this.DnsNameServer);

            this.DnsMailBox = config.GetOrDefault<string>("dnsmailbox", null);
            nodeSettings.Logger.LogDebug("DNS Seed Service mailbox set to {0}.", this.DnsMailBox);

            this.callback?.Invoke(this);
        }
    }
}
