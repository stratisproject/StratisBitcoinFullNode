using NBitcoin;

namespace Stratis.Bitcoin.Mining
{
    /// <summary>
    /// The block provider class is called by <see cref="PosMinting"/> and <see cref="PowMining"/>
    /// to create a block based on whether or not the node is mining or staking.
    /// <para>
    /// The create block logic is abstracted away from the miner or staker so that
    /// different implementations can be injected via dependency injection.
    /// </para>
    /// </summary>
    public interface IBlockProvider
    {
        /// <summary>Builds a proof of work block.</summary>
        BlockTemplate BuildPowBlock(ChainedHeader chainTip, Script script);

        /// <summary>Builds a signed proof of stake block with the next difficulty target included in the block header.</summary>
        BlockTemplate BuildPosBlock(ChainedHeader chainTip, Script script);
    }
}