using System.Security.Cryptography.X509Certificates;

namespace Stratis.Bitcoin.Features.Api
{
    public interface ICertificateStore
    {
        bool TryGet(string fileName, out X509Certificate2 certificate);
    }
}