using System.Collections.Generic;

namespace Stratis.Bitcoin.Networks.ProtocolVersion
{
    /// <summary>
    /// Class enumeration containing Stratis protocol versions, by name and version number (Id).
    /// </summary>
    /// <remarks>
    /// This enumeration class extends <see cref="ProtocolVersion"/>.
    /// </remarks>
    public class StratisProtocolVersion : ProtocolVersionBase
    {
        /// <summary>
        /// Proven headers version.
        /// </summary>
        public static readonly StratisProtocolVersion ProvenHeaders = new StratisProtocolVersion(70013, nameof(ProvenHeaders).ToLowerInvariant());

        public StratisProtocolVersion() { }

        protected StratisProtocolVersion(int id, string name) : base(id, name) { }
    }
}
