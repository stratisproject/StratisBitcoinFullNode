using System.Collections.Generic;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts.Caching
{
    public class BlockExecutionResultModel
    {
        public IStateRepositoryRoot MutatedStateRepository { get; }
        public List<Receipt> Receipts { get; }

        public BlockExecutionResultModel(IStateRepositoryRoot stateRepo, List<Receipt> receipts)
        {
            this.MutatedStateRepository = stateRepo;
            this.Receipts = receipts;
        }
    }
}
