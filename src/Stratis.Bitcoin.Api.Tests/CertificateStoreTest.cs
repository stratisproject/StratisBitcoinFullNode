using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Stratis.Bitcoin.Features.Api;
using Xunit;

namespace Stratis.Bitcoin.Api.Tests
{
    public class CertificateStoreTest : IDisposable
    {
        private const string TestCertSubjectName = "StratisTestCertName";

        public CertificateStoreTest()
        {
            RemoveTestCertificatesFromStore();
        }

        public void Dispose()
        {
            RemoveTestCertificatesFromStore();
        }

        private static void RemoveTestCertificatesFromStore()
        {
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                for(int i = 0; i < store.Certificates.Count; i++)
                {
                    if (store.Certificates[i].SubjectName.Name == $"CN={TestCertSubjectName}")
                    {
                        store.Remove(store.Certificates[i]);
                    }
                }

                store.Close();
            }
        }

        [Fact]
        public void AddToStore_WhenCertNotInSettings_AndNotInStore_CreatesAndAddsToStore()
        {
            var apiSettings = new ApiSettings();

            ICertificateStore certStore = new CertificateStore(apiSettings, StoreName.My, StoreLocation.CurrentUser);

            var certToAdd = SslCertificate.BuildSelfSignedServerCertificate(TestCertSubjectName, TestCertSubjectName);
            certStore.Add(certToAdd);

            certStore.TryGet(TestCertSubjectName, out var certificateFromStore).Should().BeTrue();
            certificateFromStore.Thumbprint.Should().Be(certToAdd.Thumbprint);
        }

        [Fact]
        public void AddToStore_WhenCertNotInSettings_AndInStore_FindsIt()
        {
            //todo: get some mug to write it
        }
    }
}
