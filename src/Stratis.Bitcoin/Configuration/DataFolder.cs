﻿using System.IO;

namespace Stratis.Bitcoin.Configuration
{
    /// <summary>
    /// Contains path locations to folders and files on disk.
    /// Used by various components of the full node.
    /// </summary>
    /// <remarks>
    /// Location name should describe if its a file or a folder.
    /// File location names end with "File" (i.e AddrMan[File]).
    /// Folder location names end with "Path" (i.e CoinView[Path]).
    /// </remarks>
    public class DataFolder
    {
        /// <summary>
        /// Initializes the path locations.
        /// </summary>
        /// <param name="settings">Node configuration.</param>
        public DataFolder(NodeSettings settings)
        {
            string path = settings.DataDir;
            this.CoinViewPath = Path.Combine(path, "coinview");
            this.AddressManagerFilePath = path;
            this.ChainPath = Path.Combine(path, "chain");
            this.BlockPath = Path.Combine(path, "blocks");
            this.IndexPath = Path.Combine(path, "index");
            this.RpcCookieFile = Path.Combine(path, ".cookie");
            this.WalletPath = Path.Combine(path);
            this.LogPath = Path.Combine(path, "Logs");
            this.DnsMasterFilePath = path;
        }

        /// <summary>Address manager's database of peers.</summary>
        /// <seealso cref="Protocol.PeerAddressManager.SavePeers(string, string)"/>
        public string AddressManagerFilePath { get; private set; }

        /// <summary>Path to the folder with coinview database files.</summary>
        /// <seealso cref="Features.Consensus.CoinViews.DBreezeCoinView.DBreezeCoinView"/>
        public string CoinViewPath { get; set; }

        /// <summary>Path to the folder with node's chain repository database files.</summary>
        /// <seealso cref="Base.BaseFeature.StartChain"/>
        public string ChainPath { get; internal set; }

        /// <summary>Path to the folder with block repository database files.</summary>
        /// <seealso cref="Features.BlockStore.BlockRepository.BlockRepository"/>
        public string BlockPath { get; internal set; }

        /// <summary>Path to the folder with block repository database files.</summary>
        /// <seealso cref="Features.IndexStore.IndexRepository.IndexRepository"/>
        public string IndexPath { get; internal set; }

        /// <summary>File to store RPC authorization cookie.</summary>
        /// <seealso cref="Features.RPC.Startup.Configure"/>
        public string RpcCookieFile { get; internal set; }

        /// <summary>Path to wallet files.</summary>
        /// <seealso cref="Features.Wallet.WalletManager.LoadWallet"/>
        public string WalletPath { get; internal set; }

        /// <summary>Path to log files.</summary>
        /// <seealso cref="Logging.LoggingConfiguration"/>
        public string LogPath { get; internal set; }

        /// <summary>Path to DNS masterfile.</summary>
        /// <seealso cref="Features.Dns.IMasterFile.Save"/>
        public string DnsMasterFilePath { get; internal set; }
    }
}
