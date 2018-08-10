using System.Security.Cryptography.X509Certificates;

namespace Stratis.Bitcoin.Features.Api
{
    public interface ICertificateStore
    {
        void Add(X509Certificate2 cert);
        bool TryGet(string name, out X509Certificate2 certificate);
        X509Certificate2 BuildSelfSignedServerCertificate(string subjectName, string password);
    }
}