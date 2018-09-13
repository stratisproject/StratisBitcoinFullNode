using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.Networks
{
    public class StratisProtocolVersion : ProtocolVersionBase
    {
        public StratisProtocolVersion ProvenHeaders = new StratisProtocolVersion(70013, nameof(ProvenHeaders).ToLowerInvariant());

        public StratisProtocolVersion()
        {
        }

        public StratisProtocolVersion(int id, string name) : base(id, name)
        {
        }

        public override IEnumerable<ProtocolVersionBase> List()
        {
            var list = base.List().ToList();

            list.Add(this.ProvenHeaders);

            return list;
            
        }
    }
}
