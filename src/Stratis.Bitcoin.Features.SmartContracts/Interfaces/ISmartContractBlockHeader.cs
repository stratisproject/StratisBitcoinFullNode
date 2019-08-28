using NBitcoin;
using Stratis.SmartContracts.Core;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public interface ISmartContractBlockHeader
    {
        uint256 HashStateRoot { get; set; }

        uint256 ReceiptRoot { get; set; }

        Bloom LogsBloom { get; set; }
    }
}
