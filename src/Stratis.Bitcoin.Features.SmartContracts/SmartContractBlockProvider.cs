using NBitcoin;
using Stratis.Bitcoin.Mining;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public sealed class SmartContractBlockProvider : IBlockProvider
    {
        private readonly SmartContractBlockDefinition blockDefinition;

        public SmartContractBlockProvider(SmartContractBlockDefinition blockDefinition)
        {
            this.blockDefinition = blockDefinition;
        }

        /// <inheritdoc/>
        public BlockTemplate BuildPosBlock(ChainedHeader chainTip, Script script)
        {
            throw new System.NotImplementedException();
        }

        /// <inheritdoc/>
        public BlockTemplate BuildPowBlock(ChainedHeader chainTip, Script script)
        {
            return this.blockDefinition.Build(chainTip, script);
        }
    }
}