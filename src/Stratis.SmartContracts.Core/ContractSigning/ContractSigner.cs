using System;
using NBitcoin;
using NBitcoin.Crypto;

namespace Stratis.SmartContracts.Core.ContractSigning
{
    /// <summary>
    /// Very basic but allows us to easily replace signing functionality at any point in the future. 
    /// </summary>
    public class ContractSigner : IContractSigner
    {
        public byte[] Sign(Key privKey, byte[] contractCode)
        {
            return privKey.SignMessageBytes(contractCode).ToDER();
        }

        public bool Verify(PubKey pubKey, byte[] contractCode, byte[] signature)
        {
            try
            {
                var ecdsaSig = new ECDSASignature(signature);
                return pubKey.VerifyMessage(contractCode, ecdsaSig);
            }
            catch (Exception)
            {
                // new ECDSASignature can throw a format exception
                // PubKey.VerifyMessage can throw unknown exceptions
                return false;
            }
        }
    }
}
