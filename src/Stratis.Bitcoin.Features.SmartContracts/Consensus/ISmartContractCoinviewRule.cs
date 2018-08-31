using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus
{
    public interface ISmartContractCoinviewRule
    {
        ISmartContractExecutorFactory ExecutorFactory { get; }
        IContractStateRoot OriginalStateRoot { get; }
        IReceiptRepository ReceiptRepository { get; }
        ISenderRetriever SenderRetriever { get; }
    }
}