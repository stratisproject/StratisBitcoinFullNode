using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace City
{
    public class CityPosConsensusOptions : PosConsensusOptions
    {
        public CityPosConsensusOptions(uint maxBlockBaseSize,
            int maxStandardVersion,
            int maxStandardTxWeight,
            int maxBlockSigopsCost,
            int provenHeadersActivationHeight) : base(maxBlockBaseSize, maxStandardVersion, maxStandardTxWeight, maxBlockSigopsCost, provenHeadersActivationHeight)
        {
        }

        public override int GetStakeMinConfirmations(int height, Network network)
        {
            if (network.Name.ToLowerInvariant().Contains("test"))
                return 20;

            return 500;
        }
    }
}
