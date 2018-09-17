using System.Collections.Generic;

namespace Stratis.Bitcoin.Networks.ProtocolVersion
{
    public interface IProtocolVersion
    {
        string Name { get; }

        int Id { get; }
    }
}