using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// Responsible for managing the whitelist used by the DNS server as a master file.
    /// </summary>
    public class WhitelistManager : IWhitelistManager
    {
        /// <summary>
        /// Defines the provider for the datetime.
        /// </summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>
        /// Defines the logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Defines the manager implementation for peer addresses used to populate the whitelist.
        /// </summary>
        private readonly IPeerAddressManager peerAddressManager;

        /// <summary>
        /// Defines the DNS server.
        /// </summary>
        private readonly IDnsServer dnsServer;

        /// <summary>
        /// Defines the peroid in seconds that the peer should last have been seen to be included in the whitelist.
        /// </summary>
        private int dnsPeerBlacklistThresholdInSeconds;

        /// <summary>
        /// Defines the DNS host name of the DNS Server.
        /// </summary>
        private string dnsHostName;

        /// <summary>
        /// Defines the DNS Settings.
        /// </summary>
        private readonly DnsSettings dnsSettings;

        private readonly IPeerBanning peerBanning;

        /// <summary>
        /// Defines the external endpoint for the dns node.
        /// </summary>
        private readonly IPEndPoint externalEndpoint;

        /// <summary>
        /// Defines if DNS server daemon is running as full node <c>true</c> or not <c>false</c>.
        /// </summary>
        private bool fullNodeMode;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhitelistManager"/> class.
        /// </summary>
        /// <param name="dateTimeProvider">The provider for datetime.</param>
        /// <param name="loggerFactory">The factory to create the logger.</param>
        /// <param name="peerAddressManager">The manager implementation for peer addresses.</param>
        /// <param name="dnsServer">The DNS server.</param>
        /// <param name="connectionSettings">The connection settings.</param>
        /// <param name="dnsSettings">The DNS settings.</param>
        /// <param name="peerBanning">Peer banning component.</param>
        public WhitelistManager(IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, IPeerAddressManager peerAddressManager, IDnsServer dnsServer, ConnectionManagerSettings connectionSettings, DnsSettings dnsSettings, IPeerBanning peerBanning)
        {
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(peerAddressManager, nameof(peerAddressManager));
            Guard.NotNull(dnsServer, nameof(dnsServer));
            Guard.NotNull(dnsSettings, nameof(dnsSettings));
            Guard.NotNull(connectionSettings, nameof(connectionSettings));
            Guard.NotNull(peerBanning, nameof(peerBanning));

            this.dateTimeProvider = dateTimeProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.peerAddressManager = peerAddressManager;
            this.dnsServer = dnsServer;
            this.dnsSettings = dnsSettings;
            this.externalEndpoint = connectionSettings.ExternalEndpoint;
            this.peerBanning = peerBanning;
        }

        /// <summary>
        /// Refreshes the managed whitelist.
        /// </summary>
        public void RefreshWhitelist()
        {
            this.dnsPeerBlacklistThresholdInSeconds = this.dnsSettings.DnsPeerBlacklistThresholdInSeconds;
            this.dnsHostName = this.dnsSettings.DnsHostName;
            this.fullNodeMode = this.dnsSettings.DnsFullNode;

            DateTimeOffset activePeerLimit = this.dateTimeProvider.GetTimeOffset().AddSeconds(-this.dnsPeerBlacklistThresholdInSeconds);

            IEnumerable<PeerAddress> whitelist = this.peerAddressManager.Peers.Where(p => p.LastSeen > activePeerLimit);

            if (!this.fullNodeMode)
            {
                // Exclude the current external ip address from DNS as its not a full node.
                whitelist = whitelist.Where(p => !p.Endpoint.Match(this.externalEndpoint));
            }

            var resourceRecords = new List<IResourceRecord>();

            foreach (PeerAddress whitelistEntry in whitelist)
            {
                if (this.peerBanning.IsBanned(whitelistEntry.Endpoint))
                {
                    this.logger.LogDebug("{0}:{1} is banned, therefore removing from masterfile.", whitelistEntry.Endpoint.Address, whitelistEntry.Endpoint.Port);
                    continue;
                }

                var domain = new Domain(this.dnsHostName);

                // Is this an IPv4 embedded address? If it is, make sure an 'A' record is added to the DNS master file, rather than an 'AAAA' record.
                if (whitelistEntry.Endpoint.Address.IsIPv4MappedToIPv6)
                {
                    IPAddress ipv4Address = whitelistEntry.Endpoint.Address.MapToIPv4();
                    var resourceRecord = new IPAddressResourceRecord(domain, ipv4Address);
                    resourceRecords.Add(resourceRecord);
                }
                else
                {
                    var resourceRecord = new IPAddressResourceRecord(domain, whitelistEntry.Endpoint.Address);
                    resourceRecords.Add(resourceRecord);
                }
            }

            IMasterFile masterFile = new DnsSeedMasterFile(resourceRecords);

            this.dnsServer.SwapMasterfile(masterFile);
        }
    }
}