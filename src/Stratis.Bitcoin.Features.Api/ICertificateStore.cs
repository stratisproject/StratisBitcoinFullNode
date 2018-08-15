using System.Security;
using System.Security.Cryptography.X509Certificates;

namespace Stratis.Bitcoin.Features.Api
{
    public interface ICertificateStore
    {
        IPasswordReader PasswordReader { get; }
        bool TryGet(string fileName, out X509Certificate2 certificate);

        X509Certificate2 BuildSelfSignedServerCertificate(SecureString password);
        void Save(X509Certificate2 certificate, string fileName, SecureString password);
    }
}