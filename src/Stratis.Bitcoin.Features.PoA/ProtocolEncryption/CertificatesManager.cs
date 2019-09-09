using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.ProtocolEncryption
{
    public class CertificatesManager
    {
        public const string ClientCertificateName = "ClientCertificate.pfx";

        public const string AuthorityCertificateName = "AuthorityCertificate.pfx";

        /// <summary>Root certificate of the certificate authority for the current network.</summary>
        public X509Certificate2 AuthorityCertificate { get; private set; }

        /// <summary>Client certificate that is used to establish connections with other peers.</summary>
        public X509Certificate2 ClientCertificate { get; private set; }

        private readonly DataFolder dataFolder;

        private readonly ILogger logger;

        public CertificatesManager(DataFolder dataFolder, ILoggerFactory loggerFactory)
        {
            this.dataFolder = dataFolder;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>Loads client and authority certificates and validates them.</summary>
        public void Initialize()
        {
            //TODO
            //this.ClientCertificate = Guard.NotNull(clientCertificate, nameof(clientCertificate));
            //this.AuthorityCertificate = Guard.NotNull(CACertificate, nameof(CACertificate));
            //
            //bool clientCertValid = this.IsSignedByAuthorityCertificate(clientCertificate, this.AuthorityCertificate);
            //
            //if (!clientCertValid)
            //    throw new Exception("Provided client certificate isn't signed by the authority certificate!");

            // TODO check for revocation here maybe?
            // TODO check client certificate contains private key
        }

        /// <summary>
        /// Checks if given certificate is signed by the authority certificate.
        /// </summary>
        /// <exception cref="Exception">Thrown in case authority chain build failed.</exception>
        private bool IsSignedByAuthorityCertificate(X509Certificate2 certificateToValidate, X509Certificate2 authorityCertificate)
        {
            var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            chain.ChainPolicy.VerificationTime = DateTime.Now;
            chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 0, 0);

            chain.ChainPolicy.ExtraStore.Add(authorityCertificate);

            bool isChainValid = chain.Build(certificateToValidate);

            if (!isChainValid)
            {
                string[] errors = chain.ChainStatus.Select(x => $"{x.StatusInformation.Trim()} ({x.Status})").ToArray();
                string certificateErrorsString = "Unknown errors.";

                if (errors.Length > 0)
                    certificateErrorsString = string.Join(", ", errors);

                throw new Exception("Trust chain did not complete to the known authority anchor. Errors: " + certificateErrorsString);
            }

            // This piece makes sure it actually matches your known root
            bool valid = chain.ChainElements.Cast<X509ChainElement>().Any(x => x.Certificate.Thumbprint == authorityCertificate.Thumbprint);

            return valid;
        }
    }
}
