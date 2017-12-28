using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractConsensusLoop : IConsensusLoop
    {
        // DI YO!
        public SmartContractConsensusLoop()
        {

        }

        public Task AcceptBlockAsync(BlockValidationContext blockValidationContext)
        {
            throw new NotImplementedException();
        }

        public Task FlushAsync(bool force)
        {
            throw new NotImplementedException();
        }

        public uint256[] GetIdsToFetch(Block block, bool enforceBIP30)
        {
            throw new NotImplementedException();
        }

        public Task StartAsync()
        {
            // Should start a loop to continually pull blocks from others.
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public Task ValidateAndExecuteBlockAsync(RuleContext context)
        {
            throw new NotImplementedException();
        }

        public void ValidateBlock(RuleContext context, bool skipRules = false)
        {
            throw new NotImplementedException();
        }
    }
}
