﻿namespace NBitcoin.Protocol
{
    /// <summary>
    /// Ask for the mempool, followed by inv messages.
    /// </summary>
    [Payload("mempool")]
    public class MempoolPayload : Payload
    {
    }
}