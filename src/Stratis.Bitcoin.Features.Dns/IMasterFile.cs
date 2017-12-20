﻿using System;
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
    /// This interface defines a DNS masterfile used to cache the whitelisted peers discovered by the DNS Seed service.
    /// </summary>
    public interface IMasterFile
    {
        /// <summary>
        /// Adds a <see cref="NetworkPeer"/> object to the masterfile.
        /// </summary>
        /// <param name="peer">The peer to add to the masterfile so that the IP address of the peer can be returned in a DNS resolve request.</param>
        void AddPeer(NetworkPeer peer);

        /// <summary>
        /// Adds a collection of <see cref="NetworkPeer"/> objects to the masterfile.
        /// </summary>
        /// <param name="peers">The peers to add to the masterfile so that the IP address of the peer can be returned in a DNS resolve request.</param>
        void AddPeers(IEnumerable<NetworkPeer> peers);

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
    }
}
