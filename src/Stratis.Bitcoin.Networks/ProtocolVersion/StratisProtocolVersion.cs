using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.Networks
{
    public class StratisProtocolVersion : ProtocolVersion
    {
        public static StratisProtocolVersion ProvenHeaders = new StratisProtocolVersion(70013, nameof(ProvenHeaders).ToLowerInvariant());

        public StratisProtocolVersion() { }

        protected StratisProtocolVersion(int id, string name) : base(id, name) { }

        public override IEnumerable<ProtocolVersion> List()
        {
            var list = base.List().ToList();

            list.Add(ProvenHeaders);

            return list; 
        }
    }
}
