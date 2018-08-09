using System;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Stratis.Bitcoin.Features.Api
{
    public class CertificateStore : ICertificateStore
    {
        private readonly StoreName storeName;
        private readonly StoreLocation storeLocation;

        public CertificateStore(StoreName storeName, StoreLocation storeLocation)
        {
            this.storeName = storeName;
            this.storeLocation = storeLocation;
        }

        public void Add(X509Certificate2 cert)
        {
            using (var store = new X509Store(this.storeName, this.storeLocation))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(cert);
                store.Close();
            }
        }

        public bool TryGet(string name, out X509Certificate2 certificate)
        {
            using (var store = new X509Store(this.storeName, this.storeLocation))
            {
                store.Open(OpenFlags.ReadOnly);
                foreach (X509Certificate2 cert in store.Certificates)
                {
                    if (cert.SubjectName.Name != $"CN={name}")
                        continue;

                    certificate = cert;
                    return true;
                }
                store.Close();
            }

            certificate = null;
            return false;
        }

        public X509Certificate2 BuildSelfSignedServerCertificate(string subjectName, string password)
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddIpAddress(IPAddress.Loopback);
            sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddDnsName(Environment.MachineName);

            var distinguishedName = new X500DistinguishedName($"CN={subjectName}");

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

                var certificate = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)),
                    new DateTimeOffset(DateTime.UtcNow.AddDays(3650)));

                return new X509Certificate2(certificate.Export(X509ContentType.Pfx, password), password,
                    X509KeyStorageFlags.MachineKeySet);
            }
        }
    }
}