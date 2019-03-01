using System.Collections.Generic;
using Stratis.Features.FederatedPeg.Models;

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
    }
}
