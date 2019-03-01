using System.Collections.Generic;
using System.Threading.Tasks;
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
        Task<bool> SaveDepositsAsync(IList<MaturedBlockDepositsModel> maturedBlockDeposits);
    }
}
