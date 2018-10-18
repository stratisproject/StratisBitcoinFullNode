using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus
{
    public interface ISmartContractCoinviewRule
    {
        IContractExecutorFactory ExecutorFactory { get; }
        Network Network { get; }
        IStateRepositoryRoot OriginalStateRoot { get; }
        IReceiptRepository ReceiptRepository { get; }
        ISenderRetriever SenderRetriever { get; }
    }
}