using NBitcoin;

namespace Stratis.SmartContracts.Core
{
    public interface ISmartContractCarrier
    {
        uint160 Sender { get; set; }

        uint160 ContractAddress { get; }

        OpcodeType OpCodeType { get; }

        ulong Value { get; }

        uint256 TransactionHash { get; }

        uint Nvout { get; }
    }
}
