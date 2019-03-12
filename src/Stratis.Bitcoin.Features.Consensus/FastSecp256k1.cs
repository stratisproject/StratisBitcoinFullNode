using System;
using Secp256k1Net;

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// Class utilizes Secp256k1.NET library to access optimized C library for EC operations on curve secp256k1
    /// </summary>
    public class FastSecp256k1
    {
        private const int PUBLIC_KEY_SIZE = 64;
        private const int SIGNATURE_RSV_SIZE = 65;

        private readonly Secp256k1 secp256k1;

        public FastSecp256k1()
        {
            this.secp256k1 = new Secp256k1();
        }

        /// <summary>
        /// Verify an ECDSA signature in DER format
        /// </summary>
        /// <param name="publicKey">Public key</param>
        /// <param name="messageHash">32-byte message hash being verified</param>
        /// <param name="signatureInDer">Signature being verified (in DER format)</param>
        /// <returns>True if the signature is correct, false the signature is incorrect or unparseable.</returns>
        public bool VerifyData(byte[] publicKey, byte[] messageHash, byte[] signatureInDer)
        {
            Span<byte> signature = new byte[SIGNATURE_RSV_SIZE];

            if (!this.secp256k1.SignatureParseDer(signature, signatureInDer))
            {
                throw new InvalidOperationException("Unmanaged EC library failed to parse signature.");
            }

            Span<byte> parsedPublicKeyData = new byte[PUBLIC_KEY_SIZE];

            if (!this.secp256k1.PublicKeyParse(parsedPublicKeyData, publicKey))
            {
                throw new InvalidOperationException("Unmanaged EC library failed to parse public key when verifying signature.");
            }

            return this.secp256k1.Verify(signature, messageHash, parsedPublicKeyData);
        }
    }

}

