﻿using System;
using NBitcoin;

namespace Stratis.Bitcoin.Features.LightWallet.Blocks
{
    /// <summary>
    /// This service starts an async loop task that periodically deletes from the blockstore.
    /// <para>
    /// If the height of the node's block store is more than <see cref="LightWalletBlockStoreService.MaxBlocksToKeep"/>, the node will 
    /// be pruned, leaving a margin of <see cref="LightWalletBlockStoreService.MaxBlocksToKeep"/> in the block database.
    /// </para>
    /// <para>
    /// For example if the block store's height is 5000, the node will be pruned up to height 4000, meaning that 1000 blocks will be kept on disk.
    /// </para>
    /// </summary>
    public interface ILightWalletBlockStoreService : IDisposable
    {
        /// <summary>
        ///  This is the header of where the node has been pruned up to.
        ///  <para>
        ///  It should be noted that deleting (pruning) blocks from the repository only removes the reference, it does not decrease the actual size on disk.
        ///  </para>
        /// </summary>
        ChainedHeader PrunedUpToHeaderTip { get; }

        /// <summary>
        /// Starts an async loop task that periodically deletes from the blockstore.
        /// </summary>
        void Start();
    }
}
