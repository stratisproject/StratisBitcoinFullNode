﻿namespace NBitcoin.Protocol
{
    [Payload("verack")]
    public class VerAckPayload : Payload, IBitcoinSerializable
    {
        #region IBitcoinSerializable Members

        public override void ReadWriteCore(BitcoinStream stream)
        {
        }

        #endregion
    }
}