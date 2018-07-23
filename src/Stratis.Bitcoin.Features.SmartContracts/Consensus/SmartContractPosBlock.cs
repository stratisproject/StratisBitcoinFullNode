using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus
{
    /// <summary>
    /// A smart contract proof of stake block that contains the additional block signature serialization.
    /// </summary>
    public class SmartContractPosBlock : PosBlock
    {
        internal SmartContractPosBlock(SmartContractPosBlockHeader blockHeader) : base(blockHeader)
        {
        }
    }
}