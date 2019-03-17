using System.IO;
using System.Net;
using NBitcoin;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Utilities;

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
        /// <param name="path">The data directory root path.</param>
        public DataFolder(string path)
        {
            this.CoinViewPath = Path.Combine(path, "coinview");
            this.AddressManagerFilePath = path;
            this.ChainPath = Path.Combine(path, "chain");
            this.KeyValueRepositoryPath = Path.Combine(path, "common");
            this.BlockPath = Path.Combine(path, "blocks");
            this.PollsPath = Path.Combine(path, "polls");
            this.IndexPath = Path.Combine(path, "index");
            this.RpcCookieFile = Path.Combine(path, ".cookie");
            this.WalletPath = Path.Combine(path);
            this.LogPath = Path.Combine(path, "Logs");
            this.ApplicationsPath = Path.Combine(path, "apps");
            this.DnsMasterFilePath = path;
            this.SmartContractStatePath = Path.Combine(path, "contracts");
            this.ProvenBlockHeaderPath = Path.Combine(path, "provenheaders");
            this.RootPath = path;
        }

        /// <summary>
        /// The DataFolder's path.
        /// </summary>
        public string RootPath { get; }

        /// <summary>Address manager's database of peers.</summary>
        /// <seealso cref="PeerAddressManager.SavePeers(string, string)"/>
        public string AddressManagerFilePath { get; private set; }

        /// <summary>Path to the folder with coinview database files.</summary>
        /// <seealso cref="Consensus.CoinViews.DBreezeCoinView.DBreezeCoinView"/>
        public string CoinViewPath { get; set; }

        /// <summary>Path to the folder with node's chain repository database files.</summary>
        /// <seealso cref="Base.BaseFeature.StartChain"/>
        public string ChainPath { get; internal set; }

        /// <summary>Path to the folder with separated key-value items managed by <see cref="IKeyValueRepository"/>.</summary>
        public string KeyValueRepositoryPath { get; internal set; }

        /// <summary>Path to the folder with block repository database files.</summary>
        /// <seealso cref="Features.BlockStore.BlockRepository.BlockRepository"/>
        public string BlockPath { get; internal set; }

        /// <summary>Path to the folder with polls.</summary>
        public string PollsPath { get; internal set; }

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
        /// <seealso cref="Dns.IMasterFile.Save"/>
        public string DnsMasterFilePath { get; internal set; }

        /// <summary>Path to the folder with smart contract state database files.</summary>
        public string SmartContractStatePath { get; set; }

        /// <summary>Path to the folder for <see cref="ProvenBlockHeader"/> items database files.</summary>
        public string ProvenBlockHeaderPath { get; set; }

        /// <summary>Path to Stratis applications</summary>
        public string ApplicationsPath { get; internal set; }
    }
}
