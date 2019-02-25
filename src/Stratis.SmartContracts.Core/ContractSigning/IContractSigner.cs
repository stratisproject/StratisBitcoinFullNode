using NBitcoin;

namespace Stratis.SmartContracts.Core.ContractSigning
{
    /// <summary>
    /// Signs and verifies contract code.
    /// </summary>
    public interface IContractSigner
    {
        /// <summary>
        /// Gets bytes representing contract code signed with a private key.
        /// </summary>
        /// <param name="privKey">Private key to sign contract code with.</param>
        /// <param name="contractCode">Contract code to sign.</param>
        /// <returns>Signature of code.</returns>
        byte[] Sign(Key privKey, byte[] contractCode);

        /// <summary>
        /// Checks whether contract code was actually signed with a given key.
        /// </summary>
        /// <param name="pubKey">The public key matching the private key used to sign code.</param>
        /// <param name="contractCode">The contract code that was signed.</param>
        /// <param name="signature">The signature to check the code against.</param>
        /// <returns>Whether the signature did actually come from the owner of the given public key.</returns>
        bool Verify(PubKey pubKey, byte[] contractCode, byte[] signature);
    }
}
