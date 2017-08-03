using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
            this.AddrManFile = Path.Combine(path, "addrman.dat");
            this.ChainPath = Path.Combine(path, "chain");
            this.BlockPath = Path.Combine(path, "blocks");
            this.IndexPath = Path.Combine(path, "index");
            this.RPCCookieFile = Path.Combine(path, ".cookie");
            this.WalletPath = Path.Combine(path);
            this.LogPath = Path.Combine(path, "Logs");
        }

        /// <summary>Address manager's database of peers.</summary>
        /// <seealso cref="NBitcoin.Protocol.AddressManager.LoadPeerFile"/>
        public string AddrManFile
        {
            get;
            set;
        }

        /// <summary>Path to the folder with coin view database files.</summary>
        /// <seealso cref="Features.Consensus.CoinViews.DBreezeCoinView.DBreezeCoinView"/>
        public string CoinViewPath
        {
            get; set;
        }

        /// <summary>Path to the folder with node's chain repository database files.</summary>
        /// <seealso cref="Base.BaseFeature.StartChain"/>
        public string ChainPath
        {
            get;
            internal set;
        }

        /// <summary>Path to the folder with block repository database files.</summary>
        /// <seealso cref="Features.BlockStore.BlockRepository.BlockRepository"/>
        public string BlockPath
        {
            get;
            internal set;
        }

        /// <summary>Path to the folder with block repository database files.</summary>
        /// <seealso cref="Features.IndexStore.IndexRepository.IndexRepository"/>
        public string IndexPath
        {
            get;
            internal set;
        }
        /// <summary>File to store RPC authorization cookie.</summary>
        /// <seealso cref="Features.RPC.Startup.Configure"/>
        public string RPCCookieFile
        {
            get;
            internal set;
        }

        /// <summary>Path to wallet files.</summary>
        /// <seealso cref="Features.Wallet.WalletManager.LoadWallet"/>
        public string WalletPath
        {
            get;
            internal set;
        }

        /// <summary>Path to log files.</summary>
        /// <seealso cref="Logging.LoggingConfiguration"/>
        public string LogPath
        {
            get;
            internal set;
        }
    }
}
