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
    public static class SharedRemoteMethods
    {
        internal static Task Stop(IFullNode fullNode)
        {
            if (fullNode != null)
            {
                fullNode.Dispose();
                fullNode = null;
            }
            return Task.CompletedTask;
        }

        internal static async Task<TransactionModel> GetRawTransactionAsync(
            string str_txid, bool verbose, 
            IPooledTransaction pooledTransaction, IFullNode fullNode,
            Network network, IChainState chainState, ChainBase chain)
        {
			Guard.NotNull(fullNode, nameof(fullNode));
			Guard.NotNull(network, nameof(network));
			Guard.NotNull(chainState, nameof(chainState));
			Guard.NotNull(chain, nameof(chain));
            uint256 txid;
			if (!uint256.TryParse(str_txid, out txid))
			{
				throw new ArgumentException(nameof(str_txid));
			}
            Transaction trx = pooledTransaction != null ? await pooledTransaction.GetTransaction(txid) : null;
            if (trx == null)
            {
                var blockStore = fullNode.NodeFeature<IBlockStore>();
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

		internal static async Task<GetTxOutModel> GetTxOutAsync(string str_txid, string str_vout, bool includeMemPool,
            IPooledGetUnspentTransaction pooledGetUnspentTransaction,
		    IGetUnspentTransaction getUnspentTransaction,
		    Network network, ChainBase chain)
        {
			Guard.NotNull(pooledGetUnspentTransaction, nameof(pooledGetUnspentTransaction));
			Guard.NotNull(getUnspentTransaction, nameof(getUnspentTransaction));
			Guard.NotNull(network, nameof(network));
			Guard.NotNull(chain, nameof(chain));
            uint256 trxid;
			if (!uint256.TryParse(str_txid, out trxid))
			{
				throw new ArgumentException(nameof(str_txid));
			}
			uint vout;
			if (!uint.TryParse(str_vout, out vout))
            {
                throw new ArgumentException(nameof(str_vout));
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

		internal static int GetBlockCount(IConsensusLoop consensusLoop)
        {
            return consensusLoop?.Tip.Height ?? -1;
        }

		internal static GetInfoModel GetInfo(IFullNode fullNode, NodeSettings nodeSettings,
		    IChainState chainState, IConnectionManager connectionManager, Network network,
		    INetworkDifficulty networkDifficulty)
        {
            Guard.NotNull(fullNode, nameof(fullNode));
			Guard.NotNull(network, nameof(network));
            var model = new GetInfoModel
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

		internal static BlockHeaderModel GetBlockHeader(string hash, bool isJsonFormat, ILogger logger,
		    ChainBase chain)
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
                var blockHeader = chain.GetBlock(uint256.Parse(hash))?.Header;
				if (blockHeader != null)
				{
					model = new BlockHeaderModel(blockHeader);
				}
            }
            return model;
        }

		internal static ValidatedAddress ValidateAddress(string address, Network network)
        {
			Guard.NotNull(network, nameof(network));
			if (string.IsNullOrEmpty(address))
			{
				throw new ArgumentNullException("address");
			}
            var res = new ValidatedAddress();
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

		internal static bool AddNode(string str_endpoint, string command, IConnectionManager connectionManager) {
			Guard.NotNull(connectionManager, nameof(connectionManager));
			if (string.IsNullOrEmpty(str_endpoint))
            {
                throw new ArgumentNullException("str_endpoint");
            }
			if (string.IsNullOrEmpty(command))
            {
                throw new ArgumentNullException("command");
            }
            IPEndPoint endpoint = IPExtensions.ToIPEndPoint(str_endpoint, connectionManager.Network.DefaultPort);
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

        internal static List<Models.PeerNodeModel> GetPeerInfo(IConnectionManager connectionManager)
		{
			Guard.NotNull(connectionManager, nameof(connectionManager));
			// Connections.PeerNodeModel contained internal setters, so copied model into RPC.
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

        internal static uint256 GetBestBlockHash(IChainState chainState)
		{
			Guard.NotNull(chainState, nameof(chainState));
            return chainState?.ConsensusTip?.HashBlock;
		}
        
        internal static uint256 GetBlockHash(string str_height, IConsensusLoop consensusLoop, ChainBase chain, ILogger logger)
		{
			Guard.NotNull(consensusLoop, nameof(consensusLoop));
            Guard.NotNull(chain, nameof(chain));
			Guard.NotNull(logger, nameof(logger));
			int height;
            if(!int.TryParse(str_height, out height))
			{
				throw new ArgumentException(nameof(str_height));
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

        internal static async Task<List<uint256>> GetRawMempoolAsync(IFullNode fullNode)
		{
			Guard.NotNull(fullNode, nameof(fullNode));
			MempoolManager mempoolManager = fullNode.NodeService<MempoolManager>();
            return await mempoolManager.GetMempoolAsync();
		}

		internal static async Task<ChainedHeader> GetTransactionBlockAsync(uint256 trxid, 
		    IFullNode fullNode, ChainBase chain)
        {
			Guard.NotNull(fullNode, nameof(fullNode));
			Guard.NotNull(chain, nameof(chain));
            ChainedHeader block = null;
            var blockStore = fullNode.NodeFeature<IBlockStore>();
            uint256 blockid = blockStore != null ? await blockStore.GetTrxBlockIdAsync(trxid) : null;
			if (blockid != null)
			{
				block = chain?.GetBlock(blockid);
			}
            return block;
        }

        internal static Target GetNetworkDifficulty(INetworkDifficulty networkDifficulty)
        {
            return networkDifficulty?.GetNetworkDifficulty();
        }
    }   
}