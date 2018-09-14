using System;
using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Networks
{
    public class ProtocolVersionBase : Enumeration
    {
        public static ProtocolVersionBase ProtocolVersion = new ProtocolVersionBase(70012, nameof(ProtocolVersion).ToLowerInvariant());
        public static ProtocolVersionBase AltProtocalVersion = new ProtocolVersionBase(70000, nameof(AltProtocalVersion).ToLowerInvariant());
        public static ProtocolVersionBase InitProtoVersion = new ProtocolVersionBase(209, nameof(InitProtoVersion).ToLowerInvariant());
        public static ProtocolVersionBase MinPeerProtoVersion = new ProtocolVersionBase(209, nameof(MinPeerProtoVersion).ToLowerInvariant());
        public static ProtocolVersionBase CAddressTimeVersion = new ProtocolVersionBase(31402, nameof(CAddressTimeVersion).ToLowerInvariant());

        protected ProtocolVersionBase()
        {
        }

        protected ProtocolVersionBase(int id, string name) : base(id, name)
        {
        }

        public virtual IEnumerable<ProtocolVersionBase> List() =>
            new[] {
                ProtocolVersion,
                AltProtocalVersion,
                InitProtoVersion,
                MinPeerProtoVersion,
                CAddressTimeVersion
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
