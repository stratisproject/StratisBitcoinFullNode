namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// Send proven headers payload which informs the peer that we are only willing to sync using the proven
    /// headers and not the old type of headers.
    /// </summary>
    [Payload("sendprovhdr")]
    public class SendProvenHeadersPayload : Payload
    {
    }
}