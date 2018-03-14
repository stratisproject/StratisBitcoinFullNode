using System.Net;

namespace Stratis.Bitcoin.P2P
{
    public interface ISelfEndpointTracker
    {
        void Add(IPEndPoint ipEndPoint);
        bool IsSelf(IPEndPoint ipEndPoint);
    }
}