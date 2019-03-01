using System.Collections.Generic;
using NBitcoin;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.SourceChain;

namespace Stratis.Features.FederatedPeg.Interfaces
{
    public interface IDepositRepository
    {
        /// <summary>
        /// Get the highest block number we know about deposits for.
        /// </summary>
        int GetSyncedBlockNumber();

        /// <summary>
        /// Save deposits to disk.
        /// </summary>
        void SaveDeposits(IList<MaturedBlockDepositsModel> maturedBlockDeposits);

        /// <summary>
        /// Get the saved deposit for a given transaction id.
        /// </summary>
        Deposit GetDeposit(uint256 depositId);
    }
}
