using System.Security.Cryptography.X509Certificates;

namespace Stratis.Bitcoin.Features.Api
{
    /// <summary>
    /// An interface providing operations on certificate repositories.
    /// </summary>
    public interface ICertificateStore
    {
        /// <summary>
        /// Tries to retrieve a certificate from the file system.
        /// </summary>
        /// <param name="filePath">The full path of the certificate file.</param>
        /// <param name="certificate">The certificate, if found.</param>
        /// <returns>A value indicating whether or not the certificate has been found at the specified location.</returns>
        bool TryGet(string filePath, out X509Certificate2 certificate);
    }
}
