using NBitcoin;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public interface ISmartContractCoinviewRule
    {
        ICallDataSerializer CallDataSerializer { get; }
        IContractExecutorFactory ExecutorFactory { get; }
        Network Network { get; }
        IStateRepositoryRoot OriginalStateRoot { get; }
        IReceiptRepository ReceiptRepository { get; }
        ISenderRetriever SenderRetriever { get; }
    }
}