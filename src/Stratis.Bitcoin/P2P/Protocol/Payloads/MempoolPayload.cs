﻿namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// Ask for the mempool, followed by inv messages.
    /// </summary>
    [Payload("mempool")]
    public class MempoolPayload : Payload
    {
    }
}