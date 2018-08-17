using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.Api
{
    public class CertificateStore : ICertificateStore
    {
        private readonly ILogger logger;

        public CertificateStore(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }
        public bool TryGet(string filePath, out X509Certificate2 certificate)
        {
            try
            {
                var fileInBytes = File.ReadAllBytes(filePath);
                certificate = new X509Certificate2(fileInBytes);
                return true;
            }
            catch (Exception e)
            {
                this.logger.LogWarning(e, "Failed to read certificate at {0}", filePath);
                certificate = null;
                return false;
            }
        }
    }
}