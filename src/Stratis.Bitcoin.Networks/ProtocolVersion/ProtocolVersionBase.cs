using System;
using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Networks
{
    public class ProtocolVersionBase : Enumeration
    {
        public ProtocolVersionBase ProtocolVersion = new ProtocolVersionBase(70012, nameof(ProtocolVersion).ToLowerInvariant());
        public ProtocolVersionBase AltProtocalVersion = new ProtocolVersionBase(70000, nameof(AltProtocalVersion).ToLowerInvariant());
        public ProtocolVersionBase InitProtoVersion = new ProtocolVersionBase(209, nameof(InitProtoVersion).ToLowerInvariant());
        public ProtocolVersionBase MinPeerProtoVersion = new ProtocolVersionBase(209, nameof(MinPeerProtoVersion).ToLowerInvariant());
        public ProtocolVersionBase CAddressTimeVersion = new ProtocolVersionBase(31402, nameof(CAddressTimeVersion).ToLowerInvariant());

        protected ProtocolVersionBase()
        {
        }

        public ProtocolVersionBase(int id, string name) : base(id, name)
        {
        }

        public virtual IEnumerable<ProtocolVersionBase> List() =>
            new[] {
                this.ProtocolVersion,
                this.AltProtocalVersion,
                this.InitProtoVersion,
                this.MinPeerProtoVersion,
                this.CAddressTimeVersion
            };

        public ProtocolVersionBase FromName(string name)
        {
            ProtocolVersionBase state = List()
                .SingleOrDefault(s => string.Equals(s.Name, name, StringComparison.CurrentCultureIgnoreCase));

            if (state == null)
                ThrowException();

            return state;
        }

        public ProtocolVersionBase From(int id)
        {
            ProtocolVersionBase state = List().SingleOrDefault(s => s.Id == id);

            if (state == null)
                ThrowException();

            return state;
        }

        private void ThrowException()
        {
            throw new Exception($"Possible values for ProtocolVersion: {string.Join(",", List().Select(s => s.Name))}");
        }
    }
}
