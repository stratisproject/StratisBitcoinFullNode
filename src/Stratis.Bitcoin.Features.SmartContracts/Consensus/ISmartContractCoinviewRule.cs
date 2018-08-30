using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus
{
    public interface ISmartContractCoinviewRule
    {
        ISmartContractExecutorFactory ExecutorFactory { get; }
        IContractStateRoot OriginalStateRoot { get; }
        IReceiptRepository ReceiptRepository { get; }
    }
}