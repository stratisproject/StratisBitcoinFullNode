using NBitcoin;

namespace Stratis.Bitcoin.Features.PoA
{
    public class PoAConsensusOptions : ConsensusOptions
    {
        /// <summary>Initializes values for networks that use block size rules.</summary>
        public PoAConsensusOptions(
            uint maxBlockBaseSize,
            int maxStandardVersion,
            int maxStandardTxWeight,
            int maxBlockSigopsCost,
            int maxStandardTxSigopsCost)
                : base(maxBlockBaseSize, maxStandardVersion, maxStandardTxWeight, maxBlockSigopsCost, maxStandardTxSigopsCost)
        {
        }
    }
}
