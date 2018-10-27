using NBitcoin;

namespace City
{
    public class CityPosConsensusOptions : PosConsensusOptions
    {
        /// <summary>Coinstake minimal confirmations softfork activation height for mainnet.</summary>
        public const int CityCoinstakeMinConfirmationActivationHeightMainnet = 500000;

        /// <summary>Coinstake minimal confirmations softfork activation height for testnet.</summary>
        public const int CityCoinstakeMinConfirmationActivationHeightTestnet = 15000;

        public CityPosConsensusOptions(
            uint maxBlockBaseSize,
            int maxStandardVersion,
            int maxStandardTxWeight,
            int maxBlockSigopsCost,
            int maxStandardTxSigopsCost,
            int provenHeadersActivationHeight)
            : base(maxBlockBaseSize, maxStandardVersion, maxStandardTxWeight, maxBlockSigopsCost, maxStandardTxSigopsCost, provenHeadersActivationHeight)
        {

        }

        public override int GetStakeMinConfirmations(int height, Network network)
        {
            if (network.Name.ToLowerInvariant().Contains("test"))
            {
                return height < CityCoinstakeMinConfirmationActivationHeightTestnet ? 10 : 20;
            }

            // The coinstake confirmation minimum should be 50 until activation at height 5 million (~347 days).
            return height < CityCoinstakeMinConfirmationActivationHeightMainnet ? 50 : 500;
        }
    }
}
