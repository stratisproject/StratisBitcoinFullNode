using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SCConsensusLoop
    {
        public SCChain Chain { get; set; }

        // DI YO!
        public SCConsensusLoop(SCChain chain)
        {
            Chain = chain;
        }

        public Task AcceptBlockAsync(SCBlockValidationContext blockValidationContext)
        {
            // Execute against the consensus rules.

            // Add block to current tip


            
            throw new NotImplementedException();
        }
    }
}
