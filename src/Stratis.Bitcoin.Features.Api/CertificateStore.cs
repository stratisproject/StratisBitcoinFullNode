using System;
using System.IO;
using System.IO.Enumeration;
using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Logging;

using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.Features.Api
{
    public class CertificateStore : ICertificateStore
    {
        private readonly string storageFolder;

        private readonly ILogger logger;

        public IPasswordReader PasswordReader { get; }

        public CertificateStore(NodeSettings nodeSettings, IPasswordReader passwordReader)
        {
            this.storageFolder = nodeSettings.DataFolder.RootPath;
            this.PasswordReader = passwordReader;
            this.logger = nodeSettings.LoggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Save(X509Certificate2 certificate, string fileName, SecureString password)
        {
            var targetDirInfo = new DirectoryInfo(this.storageFolder);
            if(!targetDirInfo.Exists) targetDirInfo.Create();
            var certificateInBytes = certificate.Export(X509ContentType.Pfx, password);
            string fullPathToCertificate = Path.Combine(targetDirInfo.FullName, fileName);
            File.WriteAllBytes(fullPathToCertificate, certificateInBytes);

            this.logger.LogWarning("A certificate file has been created at {0}.", fullPathToCertificate);
            this.logger.LogWarning("Please make sure this certificate is added to your local trusted root store to remove warnings.", fullPathToCertificate);
        }

        public bool TryGet(string fileName, out X509Certificate2 certificate)
        {
            var fullPath = Path.Combine(this.storageFolder, fileName);
            var fileInfo = new FileInfo(fullPath);
            certificate = null;

            if (!fileInfo.Exists)
                return false;

            try
            {
                var fileInBytes = File.ReadAllBytes(fullPath);
                var passwordPromptMessage = $"Please type in the password for the certificate at {fullPath} (optional):";
                int maxTries = 5;
                int tryCount = 0;
                while(tryCount <= maxTries)
                {
                    try
                    {
                        using (var passwordFromConsole = tryCount == 0 
                                     ? new SecureString()
                                     : this.PasswordReader.ReadSecurePassword(passwordPromptMessage))
                        {
                            certificate = new X509Certificate2(fileInBytes, passwordFromConsole);
                            break;
                        }
                    }
                    catch (CryptographicException ex)
                    {
                        if (!ex.Message.Contains("password", StringComparison.InvariantCultureIgnoreCase))
                            throw;

                        tryCount++;
                        if (tryCount == 1)
                            this.logger.LogWarning("The certificate at {0} requires a password to be read.", fullPath);
                        else
                            this.logger.LogWarning(ex.Message);        
                    }
                }
            }
            catch (Exception exception)
            {
                this.logger.LogError(exception, "Failed to read certificate {0}", fullPath);
                return false;
            }
            return true;
        }

        public X509Certificate2 BuildSelfSignedServerCertificate(SecureString password)
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddIpAddress(IPAddress.Loopback);
            sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddDnsName(Environment.MachineName);

            var distinguishedName = new X500DistinguishedName($"CN=StratisApiSelfSigned");

            using (RSA rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(
                        X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment |
                        X509KeyUsageFlags.DigitalSignature, false));

                request.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection {new Oid("1.3.6.1.5.5.7.3.1"), new Oid("1.3.6.1.5.5.7.3.2") }, false));

                request.CertificateExtensions.Add(sanBuilder.Build());

                X509Certificate2 certificate = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)),
                    new DateTimeOffset(DateTime.UtcNow.AddDays(3650)));

                return new X509Certificate2(certificate.Export(X509ContentType.Pfx, password), password, X509KeyStorageFlags.Exportable);
            }
        }
    }
}