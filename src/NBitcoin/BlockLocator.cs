using System.Collections.Generic;

namespace NBitcoin
{
    /// <summary>
    /// Compact representation of one's chain position which can be used to find forks with another chain.
    /// </summary>
    public class BlockLocator : IBitcoinSerializable
    {
        /// <summary>Maximum number of block hashes in the locator that we will accept in a message from a peer.</summary>
        /// <seealso cref="https://lists.linuxfoundation.org/pipermail/bitcoin-dev/2018-August/016285.html"/>
        /// <seealso cref="https://github.com/bitcoin/bitcoin/pull/13907"/>
        public const int MaxLocatorSize = 101;

        public BlockLocator()
        {
        }

        private List<uint256> blocks = new List<uint256>();
        public List<uint256> Blocks { get { return this.blocks; } set { this.blocks = value; } }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{nameof(this.Blocks)}.{nameof(this.Blocks.Count)}={this.Blocks.Count}";
        }

        #region IBitcoinSerializable Members

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.blocks);
        }

        #endregion
    }
}