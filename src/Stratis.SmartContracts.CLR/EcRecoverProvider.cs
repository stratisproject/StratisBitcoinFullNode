using NBitcoin;
using NBitcoin.Crypto;
using Stratis.SmartContracts.Core.Hashing;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Holds logic for the equivalent of the ECRECOVER opcode.
    ///
    /// This is static for now but when we know more about how we are going to use it we will adjust as necessary.
    /// </summary>
    public class EcRecoverProvider : IEcRecoverProvider
    {
        private readonly Network network;

        public EcRecoverProvider(Network network)
        {
            this.network = network;
        }

        private static uint256 GetUint256FromMessage(byte[] message)
        {
            return new uint256(HashHelper.Keccak256(message));
        }

        /// <summary>
        /// Retrieves the base58 address of the signer of an ECDSA signature.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="signature">The ECDSA signature prepended with header information specifying the correct value of recId.</param>
        /// <returns>The base58 address for the signer of a signature.</returns>
        public Address GetSigner(byte[] message, byte[] signature)
        {
            // TODO: Error handling for incorrect signature format etc.

            uint256 hashedUint256 = GetUint256FromMessage(message);
            PubKey pubKey = PubKey.RecoverCompact(hashedUint256, signature);

            return pubKey.GetAddress(this.network).ToString().ToAddress(this.network);
        }

        /// <summary>
        /// Signs a message, returning an ECDSA signature.
        /// </summary>
        /// <param name="privateKey">The private key used to sign the message.</param>
        /// <param name="message">The complete message to be signed.</param>
        /// <returns>The ECDSA signature prepended with header information specifying the correct value of recId.</returns>
        public static byte[] SignMessage(Key privateKey, byte[] message)
        {
            uint256 hashedUint256 = GetUint256FromMessage(message);

            return privateKey.SignCompact(hashedUint256);
        }
    }
}
