using System.Collections.Generic;

namespace Stratis.Bitcoin.Networks.ProtocolVersion
{
    /// <summary>
    /// Class enumeration containing Bitcoin protocol versions, by name and version number (Id).
    /// </summary>
    /// <remarks>
    /// This enumeration class extends <see cref="ProtocolVersion"/>.
    /// </remarks>
    public class BitcoinProtocolVersion : ProtocolVersionBase
    {
        /// <summary>
        /// short-id-based block download starts with this version.
        /// </summary>
        public static readonly BitcoinProtocolVersion ShortIdBlocks = new BitcoinProtocolVersion(70014, nameof(ShortIdBlocks).ToLowerInvariant());

        public BitcoinProtocolVersion() { }

        public BitcoinProtocolVersion(int id, string name) : base(id, name) { }
    }
}
