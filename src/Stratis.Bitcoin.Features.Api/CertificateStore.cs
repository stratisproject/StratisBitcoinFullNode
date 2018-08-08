using System.Security.Cryptography.X509Certificates;
using Stratis.Bitcoin.Features.Api;

namespace Stratis.Bitcoin.Api.Tests
{
    public class CertificateStore : ICertificateStore
    {
        private readonly ApiSettings apiSettings;
        private readonly StoreName storeName;
        private readonly StoreLocation storeLocation;

        public CertificateStore(ApiSettings apiSettings, StoreName storeName, StoreLocation storeLocation)
        {
            this.apiSettings = apiSettings;
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
    }
}