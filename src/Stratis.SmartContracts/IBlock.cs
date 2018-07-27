namespace Stratis.SmartContracts
{
    public interface IBlock
    {
        /// <summary>
        /// The coinbase address of the current block. 
        /// The address that will receive the mining award for this block.
        /// </summary>
        Address Coinbase { get; }

        /// <summary>
        /// The height of the current block.
        /// </summary>
        ulong Number { get; }
    }
}