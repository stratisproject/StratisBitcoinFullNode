using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore.Controllers
{
    /// <summary>
    /// RPC Controller related to the block store.
    /// </summary>
    [Controller]
    public class BlockStoreRpcController : FeatureController
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Full node.</summary>
        private readonly IFullNode fullNode;

        /// <summary>Block store.</summary>
        private readonly IBlockStore blockStore;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="fullNode">Full node to offer blockstore RPC.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        /// <param name="blockStore">Blockstore feature.</param>
        public BlockStoreRpcController(IFullNode fullNode, ILoggerFactory loggerFactory, IBlockStore blockStore) : base(fullNode: fullNode)
        {
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(blockStore, nameof(blockStore));

            this.fullNode = fullNode;
            this.blockStore = blockStore;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);            
        }

        [ActionName("getblock")]
        [ActionDescription("Returns the block in hex, given a block hash.")]
        public async Task<string> GetBlockAsync(uint256 blockHash)
        {
            this.logger.LogTrace("({0}:{1})", nameof(blockHash), blockHash);
            Block block = await this.blockStore.GetBlockAsync(blockHash);
            string blockHex = block.ToHex(this.fullNode.Network);
            this.logger.LogTrace("(-):{0}", blockHex);
            return blockHex;
        }
    }
}
