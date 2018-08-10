using System;
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
        public void AddToStore_WhenNotInStore_CreatesAndAddsToStore_Then_TryGetFindsIt()
        {
            ICertificateStore certStore = new CertificateStore(StoreName.My, StoreLocation.CurrentUser);

            certStore.TryGet(TestCertSubjectName, out var _)
                .Should().BeFalse();

            var certToAdd = certStore.BuildSelfSignedServerCertificate(TestCertSubjectName, "password");
            certStore.Add(certToAdd);

            certStore.TryGet(TestCertSubjectName, out var certificateFromStore)
                .Should().BeTrue();

            certificateFromStore.Thumbprint.Should().Be(certToAdd.Thumbprint);
        }
    }
}
