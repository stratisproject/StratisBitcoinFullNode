using System.Security.Cryptography.X509Certificates;

namespace Stratis.Bitcoin.Api.Tests
{
    public interface ICertificateStore
    {
        void Add(X509Certificate2 cert);
        bool TryGet(string name, out X509Certificate2 certificate);
    }
}