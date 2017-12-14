﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities.FileStorage;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>Contract for <see cref="PeerAddressManager"/>.</summary>
    public interface IPeerAddressManager : IDisposable
    {
        /// <summary>Data folder of where the json peer file is located.</summary>
        DataFolder PeerFilePath { get; set; }

        /// <summary>Key value store that indexes all discovered peers by their end point.</summary>
        ConcurrentDictionary<IPEndPoint, PeerAddress> Peers { get; }

        /// <summary>
        /// Adds a peer to the <see cref="Peers"/> dictionary.
        /// <para>
        /// Only routable IP addresses will be added. See <see cref="IpExtensions.IsRoutable(IPAddress, bool)"/>.
        /// </para>
        /// </summary>
        void AddPeer(NetworkAddress networkAddress, IPAddress source);

        /// <summary>
        /// Add a set of peers to the <see cref="Peers"/> dictionary.
        /// <para>
        /// Only routable IP addresses will be added. <see cref="IpExtensions.IsRoutable(IPAddress, bool)"/>
        /// </para>
        /// </summary>
        void AddPeers(NetworkAddress[] networkAddress, IPAddress source);

        /// <summary> Find a peer by endpoint.</summary>
        PeerAddress FindPeer(IPEndPoint endPoint);

        /// <summary>Loads peers from a json formatted file on disk.</summary>
        void LoadPeers();

        /// <summary>Persist peers to disk in json format.</summary>
        void SavePeers();

        /// <summary>
        /// A connection attempt was made to a peer.
        /// <para>
        /// Increments <see cref="PeerAddress.ConnectionAttempts"/> of the peer as well as the <see cref="PeerAddress.LastConnectionSuccess"/>
        /// </para>
        /// </summary>
        void PeerAttempted(IPEndPoint endpoint, DateTimeOffset peerAttemptedAt);

        /// <summary>
        /// A peer was successfully connected to.
        /// <para>
        /// Resets the <see cref="PeerAddress.ConnectionAttempts"/> and <see cref="PeerAddress.LastConnectionAttempt"/> of the peer.
        /// Sets the peer's <see cref="PeerAddress.LastConnectionSuccess"/> to now.
        /// </para>
        /// </summary>
        void PeerConnected(IPEndPoint endpoint, DateTimeOffset peerAttemptedAt);

        /// <summary>
        /// A version handshake between two peers was successful.
        /// <para>
        /// Sets the peer's <see cref="PeerAddress.LastConnectionHandshake"/> time to now.
        /// </para>
        /// </summary>
        void PeerHandshaked(IPEndPoint endpoint, DateTimeOffset peerAttemptedAt);

        /// <summary>Peer selector instance, used to select peers to connect to.</summary>
        IPeerSelector PeerSelector { get; }
    }

    /// <summary>
    /// This manager keeps a set of peers discovered on the network in cache and on disk.
    /// <para>
    /// The manager updates peer state according to how recent they have been connected to or not.
    /// </para>
    /// </summary>
    public sealed class PeerAddressManager : IPeerAddressManager
    {
        /// <inheritdoc />
        public ConcurrentDictionary<IPEndPoint, PeerAddress> Peers { get; private set; }

        /// <summary>The file name of the peers file.</summary>
        internal const string PeerFileName = "peers.json";

        /// <inheritdoc />
        public DataFolder PeerFilePath { get; set; }

        /// <inheritdoc />
        public IPeerSelector PeerSelector { get; private set; }

        /// <summary>Constructor used by unit tests.</summary>
        /// <summary>Peer selector instance, used to select peers to connect to.</summary>
        public IPeerSelector Selector { get; private set; }

        public PeerAddressManager()
        {
            this.Peers = new ConcurrentDictionary<IPEndPoint, PeerAddress>();
            this.PeerSelector = new PeerSelector(this.Peers);
        }

        /// <summary>Constructor used by dependency injection.</summary>
        /// <param name="peerFilePath">The file path the peer file is saved to.</param>
        public PeerAddressManager(DataFolder peerFilePath)
            : this()
        {
            this.PeerFilePath = peerFilePath;
        }

        /// <inheritdoc />
        public void LoadPeers()
        {
            var fileStorage = new FileStorage<List<PeerAddress>>(this.PeerFilePath.AddressManagerFilePath);
            var peers = fileStorage.LoadByFileName(PeerFileName);
            peers.ForEach(peer =>
            {
                this.Peers.TryAdd(peer.NetworkAddress.Endpoint, peer);
            });
        }

        /// <inheritdoc />
        public void SavePeers()
        {
            if (this.Peers.Any() == false)
                return;

            var fileStorage = new FileStorage<List<PeerAddress>>(this.PeerFilePath.AddressManagerFilePath);
            fileStorage.SaveToFile(this.Peers.OrderByDescending(p => p.Value.LastConnectionSuccess).Select(p => p.Value).ToList(), PeerFileName);
        }

        /// <inheritdoc/>
        public void AddPeer(NetworkAddress networkAddress, IPAddress source)
        {
            if (networkAddress.Endpoint.Address.IsRoutable(true) == false)
                return;

            var peerToAdd = PeerAddress.Create(networkAddress, source);
            this.Peers.TryAdd(peerToAdd.NetworkAddress.Endpoint, peerToAdd);
        }

        /// <inheritdoc/>
        public void AddPeers(NetworkAddress[] networkAddresses, IPAddress source)
        {
            foreach (var networkAddress in networkAddresses)
            {
                this.AddPeer(networkAddress, source);
            }
        }

        /// <inheritdoc/>
        public void PeerAttempted(IPEndPoint endpoint, DateTimeOffset peerAttemptedAt)
        {
            var peer = this.FindPeer(endpoint);
            if (peer == null)
                return;

            peer.SetAttempted(peerAttemptedAt);
        }

        /// <inheritdoc/>
        public void PeerConnected(IPEndPoint endpoint, DateTimeOffset peerConnectedAt)
        {
            var peer = this.FindPeer(endpoint);
            if (peer == null)
                return;

            peer.SetConnected(peerConnectedAt);
        }

        /// <inheritdoc/>
        public void PeerHandshaked(IPEndPoint endpoint, DateTimeOffset peerHandshakedAt)
        {
            var peer = this.FindPeer(endpoint);
            if (peer == null)
                return;

            peer.SetHandshaked(peerHandshakedAt);
        }

        /// <inheritdoc/>
        public PeerAddress FindPeer(IPEndPoint endPoint)
        {
            var peer = this.Peers.Skip(0).SingleOrDefault(p => p.Key.Match(endPoint));
            if (peer.Value != null)
                return peer.Value;
            return null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.SavePeers();
        }
    }
}