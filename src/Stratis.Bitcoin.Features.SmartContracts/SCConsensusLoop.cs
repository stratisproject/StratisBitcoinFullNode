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
            this.Chain = chain;
        }

        public async Task AcceptBlockAsync(SCBlockValidationContext blockValidationContext)
        {
            // Execute against the consensus rules.
            SCBlock block = blockValidationContext.Block;


            // Add block to current tip

            Chain.SetTip(block);
            
            throw new NotImplementedException();
        }

        public Task ExecuteBlockAsync()
        {
            throw new NotImplementedException();
        }
    }
}
