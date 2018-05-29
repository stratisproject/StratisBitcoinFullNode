using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Features.RPC.Controllers
{
    /// <summary>
    /// This class provides a collection of shared methods for both 
    /// API and RPC to interact with the full node. 
    /// </summary>
    public class SharedRemoteMethods
    {
        /// <summary>
        /// Stops the full node.
        /// </summary>
        /// <returns>A completed <see cref="Task"/>.</returns>
        internal static Task Stop(IFullNode fullNode)
        {
            if (fullNode != null)
            {
                fullNode.Dispose();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Returns a raw transaction model given a transaction hash in simple or verbose format.
        /// </summary>
        /// <param name="strTxid">A string representing a uint256 hash</param>
        /// <param name="verbose">Boolean indicating if verbose model is wanted</param>
        /// <param name="pooledTransaction">A pooled transaction interface for the Mempool. Used to return pooled transactions.</param>
        /// <param name="fullNode">The full node. Required for IBlockstore.</param>
        /// <param name="network">The full node's network</param>
        /// <param name="chainState">The full node's chainstate. Used for verbose model.</param>
        /// <param name="chain">The full node's chain. Used for verbose model.</param>
        /// <returns>A <see cref="TransactionBriefModel"/> or <see cref="TransactionVerboseModel"/> from the given hash. Return <c>null</c> if transaction doesn't exist.</returns>
        /// <exception cref="ArgumentException">Thrown if strTxid is an invalid uint256.</exception>
        internal static async Task<TransactionModel> GetRawTransactionAsync(
            string strTxid, bool verbose, 
            IPooledTransaction pooledTransaction, IFullNode fullNode,
            Network network, IChainState chainState, ChainBase chain)
        {
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(chain, nameof(chain));

            uint256 txid;
            if (!uint256.TryParse(strTxid, out txid))
            {
                throw new ArgumentException(nameof(strTxid));
            }

            // First tries to find a pooledTransaction. If can't, will grab it from the blockstore if it exists. 
            Transaction trx = pooledTransaction != null ? await pooledTransaction.GetTransaction(txid) : null;
            if (trx == null)
            {
                IBlockStore blockStore = fullNode.NodeFeature<IBlockStore>();
                trx = blockStore != null ? await blockStore.GetTrxAsync(txid) : null;
            }

            if (trx == null)
            {
                return null;
            }

            if (verbose)
            {
                ChainedHeader block = await GetTransactionBlockAsync(txid, fullNode, chain);
                return new TransactionVerboseModel(trx, network, block, chainState?.ConsensusTip);
            }
            else
            {
                return new TransactionBriefModel(trx);
            }
        }

        /// <summary>
        /// Gets the unspent outputs of a transaction id and vout number.
        /// </summary>
        /// <param name="strTxid">The transaction id.</param>
        /// <param name="strvOut">The vout number.</param>
        /// <param name="includeMemPool">A Boolean indicating to search Mempool.</param>
        /// <param name="pooledGetUnspentTransaction">A pool of unspent transactions in Mempool.</param>
        /// <param name="getUnspentTransaction">Unspent transactions not in Mempool</param>
        /// <param name="network">The full node's network.</param>
        /// <param name="chain">The full node's chain. Used to get chaintip.</param>
        /// <returns>A <see cref="GetTxOutModel"/>, returns <c>null</c> if fails.</returns>
        /// <exception cref="ArgumentException">Throws if either strTxid is not a valid <see cref="uint256"/> or if strvOut is not an <see cref="uint"/>.</exception>
        internal static async Task<GetTxOutModel> GetTxOutAsync(string strTxid, string strvOut, bool includeMemPool,
            IPooledGetUnspentTransaction pooledGetUnspentTransaction,
            IGetUnspentTransaction getUnspentTransaction,
            Network network, ChainBase chain)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(chain, nameof(chain));
            uint256 trxid;

            if (!uint256.TryParse(strTxid, out trxid))
            {
                throw new ArgumentException(nameof(strTxid));
            }

            uint vout;
            if (!uint.TryParse(strvOut, out vout))
            {
                throw new ArgumentException(nameof(strvOut));
            }

            UnspentOutputs unspentOutputs = null;
            if (includeMemPool)
            {
                unspentOutputs = pooledGetUnspentTransaction != null ? await pooledGetUnspentTransaction.GetUnspentTransactionAsync(trxid) : null;
            }
            else
            {
                unspentOutputs = getUnspentTransaction != null ? await getUnspentTransaction.GetUnspentTransactionAsync(trxid) : null;
            }

            if (unspentOutputs == null)
            {
                return null;
            }

            return new GetTxOutModel(unspentOutputs, vout, network, chain.Tip);
        }

        /// <summary>
        /// Gets the current consensus tip height.
        /// </summary>
        /// <param name="consensusLoop">The full node's consensus loop. Used to get the tip height.</param>
        /// <returns>The current tip height. Returns <c>null</c> if fails.</returns>
        internal static int GetBlockCount(IConsensusLoop consensusLoop)
        {
            return consensusLoop?.Tip.Height ?? -1;
        }

        /// <summary>
        /// Gets general information about the full node.
        /// </summary>
        /// <param name="fullNode">The full node. Used for version info.</param>
        /// <param name="nodeSettings">The full node's settings. Used for protocol version and relay fee.</param>
        /// <param name="chainState">The full node's chain state. Used for Blocks.</param>
        /// <param name="connectionManager">The full node's connection manager. Used for timeoffset and connections.</param>
        /// <param name="network">The full node's network.</param>
        /// <param name="networkDifficulty">The network difficulty.</param>
        /// <returns>A <see cref="GetInfoModel"/>.</returns>
        internal static GetInfoModel GetInfo(Network network, 
            IFullNode fullNode = null, NodeSettings nodeSettings = null,
            IChainState chainState = null, IConnectionManager connectionManager = null,
            INetworkDifficulty networkDifficulty = null)
        {
            Guard.NotNull(network, nameof(network));
            GetInfoModel model = new GetInfoModel
            {
                Version = fullNode?.Version?.ToUint() ?? 0,
                ProtocolVersion = (uint)(nodeSettings?.ProtocolVersion ?? NodeSettings.SupportedProtocolVersion),
                Blocks = chainState?.ConsensusTip?.Height ?? 0,
                TimeOffset = connectionManager?.ConnectedPeers?.GetMedianTimeOffset() ?? 0,
                Connections = connectionManager?.ConnectedPeers?.Count(),
                Proxy = string.Empty,
                Difficulty = GetNetworkDifficulty(networkDifficulty)?.Difficulty ?? 0,
                Testnet = network.IsTest(),
                RelayFee = nodeSettings?.MinRelayTxFeeRate?.FeePerK?.ToUnit(MoneyUnit.BTC) ?? 0,
                Errors = string.Empty,

                //TODO: Wallet related infos: walletversion, balance, keypNetwoololdest, keypoolsize, unlocked_until, paytxfee
                WalletVersion = null,
                Balance = null,
                KeypoolOldest = null,
                KeypoolSize = null,
                UnlockedUntil = null,
                PayTxFee = null
            };

            return model;
        }

        /// <summary>
        /// Gets the block header of the block identified by the hash.
        /// </summary>
        /// <param name="hash">The requested block's hash.</param>
        /// <param name="isJsonFormat">Boolean for Json formatted output</param>
        /// <param name="logger">The full node's logger</param>
        /// <param name="chain">The full node's chain</param>
        /// <returns>A <see cref="BlockHeaderModel"/> corresponding to the given block hash. Returns <c>null</c> if chain is null.</returns>
        /// <exception cref="ArgumentNullException">Throws if hash is null or empty</exception>
        /// <exception cref="NotImplementedException">Throws if JsonFormat is false.</exception>
        internal static BlockHeaderModel GetBlockHeader(string hash, bool isJsonFormat, 
            ILogger logger, ChainBase chain)
        {
            Guard.NotNull(logger, nameof(logger));
            if (string.IsNullOrEmpty(hash))
            {
                throw new ArgumentNullException("hash");
            }

            logger.LogDebug("GetBlockHeader {0}", hash);
            if (!isJsonFormat)
            {
                logger.LogError("Binary serialization is not supported'{0}'.", nameof(GetBlockHeader));
                throw new NotImplementedException();
            }      
            
            BlockHeaderModel model = null;
            if (chain != null)
            {
                BlockHeader blockHeader = chain.GetBlock(uint256.Parse(hash))?.Header;
                if (blockHeader != null)
                {
                    model = new BlockHeaderModel(blockHeader);
                }
            }

            return model;
        }

        /// <summary>
        /// Returns information about a bech32 or base58 bitcoin address.
        /// </summary>
        /// <param name="address">An address string to check</param>
        /// <param name="network">The full node's network.</param>
        /// <returns>A <see cref="ValidatedAddress"/>, which contains a boolean property indicating if the address is valid.</returns>
        /// <exception cref="ArgumentNullException">Thrown if address provided is null or empty.</exception>
        internal static ValidatedAddress ValidateAddress(string address, Network network)
        {
            Guard.NotNull(network, nameof(network));
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentNullException("address");
            }

            ValidatedAddress res = new ValidatedAddress();
            res.IsValid = false;
            // P2WPKH
            if (BitcoinWitPubKeyAddress.IsValid(address, ref network, out Exception _))
            {
                res.IsValid = true;
            }
            // P2WSH
            else if (BitcoinWitScriptAddress.IsValid(address, ref network, out Exception _))
            {
                res.IsValid = true;
            }
            // P2PKH
            else if (BitcoinPubKeyAddress.IsValid(address, ref network))
            {
                res.IsValid = true;
            }
            // P2SH
            else if (BitcoinScriptAddress.IsValid(address, ref network))
            {
                res.IsValid = true;
            }

            return res;
        }

        /// <summary>
        /// Adds a node to the connection manager.
        /// </summary>
        /// <param name="strEndpoint">A valid ip endpoint in string form.</param>
        /// <param name="command">A command. {Add, remove, onetry}</param>
        /// <param name="connectionManager">The full node's connection manager.</param>
        /// <returns><c>True</c> if command successfully completed. Otherwise throws exception.</returns>
        /// <exception cref="ArgumentNullException">Thrown if either strEndpoint or command are null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown if command is invalid/not supported.</exception>
        internal static bool AddNode(string strEndpoint, string command, 
            IConnectionManager connectionManager) {
            Guard.NotNull(connectionManager, nameof(connectionManager));

            if (string.IsNullOrEmpty(strEndpoint))
            {
                throw new ArgumentNullException("strEndpoint");
            }

            if (string.IsNullOrEmpty(command))
            {
                throw new ArgumentNullException("command");
            }

            IPEndPoint endpoint = IPExtensions.ToIPEndPoint(strEndpoint, connectionManager.Network.DefaultPort);
            switch (command)
            {
                case "add":
                    connectionManager.AddNodeAddress(endpoint);
                    break;
                case "remove":
                    connectionManager.RemoveNodeAddress(endpoint);
                    break;                    
                case "onetry":
                    connectionManager.ConnectAsync(endpoint).GetAwaiter().GetResult();
                    break;
                default:
                    throw new ArgumentException("command");
            }

            return true;
        }

        /// <summary>
        /// Gets peer information from the connection manager.
        /// This method originally was contained in 
        /// Stratis.Bitcoin/Connection/ConnectionManagerController.cs 
        /// </summary>
        /// <param name="connectionManager">The full node's connection manager.</param>
        /// <returns>A list of <see cref="Models.PeerNodeModel"/> for connected peers.</returns>
        internal static List<Models.PeerNodeModel> GetPeerInfo(IConnectionManager connectionManager)
        {
            Guard.NotNull(connectionManager, nameof(connectionManager));
            // Connection.PeerNodeModel contained internal setters, therefore model into RPC/Models
            List<Models.PeerNodeModel> peerList = new List<Models.PeerNodeModel>();
            List<INetworkPeer> peers = connectionManager.ConnectedPeers.ToList();
            foreach (INetworkPeer peer in peers)
            {
                if ((peer != null) && (peer.RemoteSocketAddress != null))
                {
                    Models.PeerNodeModel peerNode = new Models.PeerNodeModel
                    {
                        Id = peers.IndexOf(peer),
                        Address = peer.RemoteSocketEndpoint.ToString()
                    };
                    if (peer.MyVersion != null)
                    {
                        peerNode.LocalAddress = peer.MyVersion.AddressReceiver?.ToString();
                        peerNode.Services = ((ulong)peer.MyVersion.Services).ToString("X");
                        peerNode.Version = (uint)peer.MyVersion.Version;
                        peerNode.SubVersion = peer.MyVersion.UserAgent;
                        peerNode.StartingHeight = peer.MyVersion.StartHeight;
                    }
                    ConnectionManagerBehavior connectionManagerBehavior = peer.Behavior<ConnectionManagerBehavior>();
                    if (connectionManagerBehavior != null)
                    {
                        peerNode.Inbound = connectionManagerBehavior.Inbound;
                        peerNode.IsWhiteListed = connectionManagerBehavior.Whitelisted;
                    }
                    if (peer.TimeOffset != null)
                    {
                        peerNode.TimeOffset = peer.TimeOffset.Value.Seconds;
                    }
                    peerList.Add(peerNode);
                }
            }

            return peerList;
        }

        /// <summary>
        /// Get the hash of the block at the consensus tip.
        /// </summary>
        /// <param name="chainState">The full node's chainstate.</param>
        /// <returns>A hash of the block at the consensus tip. Returns <c>null</c> if fails.</returns>
        internal static uint256 GetBestBlockHash(IChainState chainState = null)
        {
            return chainState?.ConsensusTip?.HashBlock;
        }

        /// <summary>
        /// Gets the hash of the block at the given height.
        /// </summary>
        /// <param name="strHeight">The height as a string.</param>
        /// <param name="consensusLoop">The full node's consensus loop.</param>
        /// <param name="chain">The full node's chain.</param>
        /// <param name="logger">The full node's logger.</param>
        /// <returns>The hash of the block at the given height. Returns <c>null</c> if fails.</returns>
        /// <exception cref="ArgumentException">Thrown if height is not a valid integer.</exception>
        internal static uint256 GetBlockHash(string strHeight, IConsensusLoop consensusLoop, 
            ChainBase chain, ILogger logger)
        {
            Guard.NotNull(consensusLoop, nameof(consensusLoop));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(logger, nameof(logger));

            int height;
            if(!int.TryParse(strHeight, out height))
            {
                throw new ArgumentException(nameof(strHeight));
            }

            logger.LogDebug("GetBlockHash {0}", height);
            uint256 bestBlockHash = consensusLoop.Tip?.HashBlock;
            ChainedHeader bestBlock = bestBlockHash == null ? null : chain.GetBlock(bestBlockHash);
            if (bestBlock == null)
            {
                return null;
            }

            ChainedHeader block = chain.GetBlock(height);
            return block == null || block.Height > bestBlock.Height ? null : block.HashBlock;
        }

        /// <summary>
        /// Lists the contents of the memory pool.
        /// </summary>
        /// <param name="fullNode">The full node. Used to access MempoolManager.</param>
        /// <returns>A list of transaction hashes in the Mempool.</returns>
        internal static async Task<List<uint256>> GetRawMempoolAsync(IFullNode fullNode)
        {
            Guard.NotNull(fullNode, nameof(fullNode));
            MempoolManager mempoolManager = fullNode.NodeService<MempoolManager>();
            List<uint256> result = await mempoolManager.GetMempoolAsync().ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// Retrieves a transaction block given a valid hash.
        /// This function is used by other methods in this class and not explicitly by RPC/API.
        /// </summary>
        /// <param name="trxid">A valid uint256 hash</param>
        /// <param name="fullNode">The full node. Used to access blockstore.</param>
        /// <param name="chain">The full node's chain. Used to get block.</param>
        /// <returns>A <see cref="ChainedHeader"/> for the given transaction hash. Returns <c>null</c> if fails.</returns>
        internal static async Task<ChainedHeader> GetTransactionBlockAsync(uint256 trxid, 
            IFullNode fullNode, ChainBase chain)
        {
            Guard.NotNull(fullNode, nameof(fullNode));

            ChainedHeader block = null;
            IBlockStore blockStore = fullNode.NodeFeature<IBlockStore>();
            uint256 blockid = blockStore != null ? await blockStore.GetTrxBlockIdAsync(trxid) : null;
            if (blockid != null)
            {
                block = chain?.GetBlock(blockid);
            }
            
            return block;
        }

        /// <summary>
        /// Gets the current network difficulty. 
        /// </summary>
        /// <param name="networkDifficulty">The full node's network difficulty.</param>
        /// <returns>A network difficulty <see cref="Target"/>. Returns <c>null</c> if fails.</returns>
        internal static Target GetNetworkDifficulty(INetworkDifficulty networkDifficulty = null)
        {
            return networkDifficulty?.GetNetworkDifficulty();
        }
    }   
}