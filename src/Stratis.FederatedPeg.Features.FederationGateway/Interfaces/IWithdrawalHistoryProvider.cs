using System.Collections.Generic;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IWithdrawalHistoryProvider
    {
        List<WithdrawalModel> GetHistory(int maximumEntriesToReturn);
    }
}
