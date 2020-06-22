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
    public static class EcRecover
    {
        // TODO: Not sure what this is yet.
        private const int RecId = 0;

        public static ECDSASignature SignMessage(Key privateKey, byte[] message)
        {
            uint256 hashedUint256 = GetUint256FromMessage(message);
            return privateKey.Sign(hashedUint256);
        }

        public static Address GetAddressFromSignatureAndMessage(byte[] signature, byte[] message, Network network)
        {
            // TODO: Error handling for incorrect signature format etc.

            uint256 hashedUint256 = GetUint256FromMessage(message);
            ECDSASignature loadedSignature = new ECDSASignature(signature);
            PubKey pubKey = ECKeyUtils.RecoverFromSignature(RecId, loadedSignature, hashedUint256, true);
            return pubKey.GetAddress(network).ToString().ToAddress(network);
        }

        private static uint256 GetUint256FromMessage(byte[] message)
        {
            return new uint256(HashHelper.Keccak256(message));
        }
    }
}
