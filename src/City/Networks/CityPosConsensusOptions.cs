using NBitcoin;

namespace City
{
    public class CityPosConsensusOptions : PosConsensusOptions
    {
        /// <summary>Coinstake minimal confirmations softfork activation height for mainnet.</summary>
        public const int CityCoinstakeMinConfirmationActivationHeightMainnet = 500000;

        /// <summary>Coinstake minimal confirmations softfork activation height for testnet.</summary>
        public const int CityCoinstakeMinConfirmationActivationHeightTestnet = 15000;

        /// <summary>
        /// Initializes the default values.
        /// </summary>
        public CityPosConsensusOptions()
        {
        }

        /// <summary>
        /// Initializes all values. Used by networks that use block weight rules.
        /// </summary>
        public CityPosConsensusOptions(
            uint maxBlockBaseSize,
            uint maxBlockWeight,
            uint maxBlockSerializedSize,
            int witnessScaleFactor,
            int maxStandardVersion,
            int maxStandardTxWeight,
            int maxBlockSigopsCost,
            int maxStandardTxSigopsCost) : base(maxBlockBaseSize, maxBlockWeight, maxBlockSerializedSize, witnessScaleFactor, maxStandardVersion, maxStandardTxWeight, maxBlockSigopsCost, maxStandardTxSigopsCost)
        {
        }

        /// <summary>
        /// Initializes values for networks that use block size rules.
        /// </summary>
        public CityPosConsensusOptions(
            uint maxBlockBaseSize,
            int maxStandardVersion,
            int maxStandardTxWeight,
            int maxBlockSigopsCost,
            int maxStandardTxSigopsCost
            ) : base(maxBlockBaseSize, maxStandardVersion, maxStandardTxWeight, maxBlockSigopsCost, maxStandardTxSigopsCost)
        {
        }

        public override int GetStakeMinConfirmations(int height, Network network)
        {
            if (network.Name.ToLowerInvariant().Contains("test"))
            {
                return height < CityCoinstakeMinConfirmationActivationHeightTestnet ? 10 : 20;
            }

            // The coinstake confirmation minimum should be 50 until activation at height 500K (~347 days).
            return height < CityCoinstakeMinConfirmationActivationHeightMainnet ? 50 : 500;
        }
    }
}
