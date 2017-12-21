using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using DNS.Server;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// This class defines a DNS masterfile used to cache the whitelisted peers discovered by the DNS Seed service that supports saving
    /// and loading from a stream.
    /// </summary>
    public class DnsSeedMasterFile : MasterFile, IMasterFile
    {
        /// <summary>
        /// Adds a <see cref="NetworkPeer"/> object to the masterfile.
        /// </summary>
        /// <param name="peer">The peer to add to the masterfile so that the IP address of the peer can be returned in a DNS resolve request.</param>
        public void AddPeer(NetworkPeer peer)
        {
            // TODO
        }

        /// <summary>
        /// Adds a collection of <see cref="NetworkPeer"/> objects to the masterfile.
        /// </summary>
        /// <param name="peers">The peers to add to the masterfile so that the IP address of the peer can be returned in a DNS resolve request.</param>
        public void AddPeers(IEnumerable<NetworkPeer> peers)
        {
            // TODO
        }

        /// <summary>
        /// Loads the saved masterfile from the specified stream.
        /// </summary>
        /// <param name="stream">The stream containing the masterfile.</param>
        public void Load(Stream stream)
        {
            // TODO
        }

        /// <summary>
        /// Saves the cached masterfile to the specified stream.
        /// </summary>
        /// <param name="stream">The stream to write the masterfile to.</param>
        public void Save(Stream stream)
        {
            // TODO
        }
    }
}
