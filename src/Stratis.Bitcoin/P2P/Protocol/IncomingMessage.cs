namespace Stratis.Bitcoin.P2P.Protocol
{
    /// <summary>
    /// A type takes context information about a <see cref="Message"/>.
    /// </summary>
    public class IncomingMessage
    {
        /// <summary>A network payload message.</summary>
        public Message Message { get; set; }

        /// <summary>The total length of the payload.</summary>
        public long Length { get; set; }
    }
}