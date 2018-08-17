using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Stratis.Bitcoin.Features.Api
{
    public class CertificateStore : ICertificateStore
    {
        public bool TryGet(string filePath, out X509Certificate2 certificate)
        {
            certificate = null;

            if (!File.Exists(filePath))
                return false;

            var fileInBytes = File.ReadAllBytes(filePath);
            certificate = new X509Certificate2(fileInBytes);
            
            return true;
        }
    }
}