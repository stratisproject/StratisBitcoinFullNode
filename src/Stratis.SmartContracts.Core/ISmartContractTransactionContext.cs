using System.Collections.Generic;
using NBitcoin;

namespace Stratis.SmartContracts.Core
{
    public interface ISmartContractTransactionContext
    {
        uint256 TransactionHash { get; }

        uint160 Sender { get; }

        ulong TxOutValue { get; }

        uint Nvout { get; }

        IEnumerable<byte> ContractData { get; }

        Money MempoolFee { get; }

        uint160 CoinbaseAddress { get; }

        ulong BlockHeight { get; }

        bool IsCreate { get; }

        bool IsCall { get; }

        // Temporary
        Transaction Transaction { get; }
    }
}
