using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.Networks
{
    /// <summary>
    /// Class enumeration containing Stratis protocol versions, by name and version number (Id).
    /// </summary>
    /// <remarks>
    /// This enumeration class extends <see cref="ProtocolVersion"/>.
    /// </remarks>
    public class StratisProtocolVersion : ProtocolVersion
    {
        /// <summary>
        /// Proven headers version.
        /// </summary>
        public static StratisProtocolVersion ProvenHeaders = new StratisProtocolVersion(70013, nameof(ProvenHeaders).ToLowerInvariant());

        public StratisProtocolVersion() { }

        protected StratisProtocolVersion(int id, string name) : base(id, name) { }

        /// <summary>
        /// List all baseline, plus new Stratis protocol versions.
        /// </summary>
        public override IEnumerable<ProtocolVersion> List()
        {
            var list = base.List().ToList();

            list.Add(ProvenHeaders);

            return list; 
        }
    }
}
