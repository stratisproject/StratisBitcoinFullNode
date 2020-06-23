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
        // TODO: Not sure what this is yet.
        private const int RecId = 1;

        private readonly Network network;

        public EcRecoverProvider(Network network)
        {
            this.network = network;
        }

        private static uint256 GetUint256FromMessage(byte[] message)
        {
            return new uint256(HashHelper.Keccak256(message));
        }

        public Address GetSigner(byte[] message, byte[] signature)
        {
            // TODO: Error handling for incorrect signature format etc.

            uint256 hashedUint256 = GetUint256FromMessage(message);
            ECDSASignature loadedSignature = new ECDSASignature(signature);
            PubKey pubKey = ECKeyUtils.RecoverFromSignature(RecId, loadedSignature, hashedUint256, true);
            return pubKey.GetAddress(this.network).ToString().ToAddress(this.network);
        }

        public static ECDSASignature SignMessage(Key privateKey, byte[] message)
        {
            uint256 hashedUint256 = GetUint256FromMessage(message);
            return privateKey.Sign(hashedUint256);
        }
    }
}
