﻿namespace NBitcoin.Protocol
{
    /// <summary>
    /// A block received after being asked with a getdata message.
    /// </summary>
    [Payload("block")]
    public class BlockPayload : BitcoinSerializablePayload<Block>
    {
        public BlockPayload()
        {
        }

        public BlockPayload(Block block)
            : base(block)
        {
        }
    }
}
