using NBitcoin;
using Stratis.Bitcoin.Features.PoA;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA
{
    public class SmartContractPoAConsensusFactory : PoAConsensusFactory
    {
        /// <inheritdoc />
        public override BlockHeader CreateBlockHeader()
        {
            return new SmartContractPoABlockHeader();
        }
    }
}
