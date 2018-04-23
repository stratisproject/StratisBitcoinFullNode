using NBitcoin;

namespace Stratis.SmartContracts.Core
{
    public interface ISmartContractCarrier
    {
        Gas GasLimit { get; }

        ulong GasCostBudget { get; }

        ulong GasPrice { get; }

        uint160 Sender { get; set; }

        OpcodeType OpCodeType { get; }

        ulong Value { get; }
    }
}
