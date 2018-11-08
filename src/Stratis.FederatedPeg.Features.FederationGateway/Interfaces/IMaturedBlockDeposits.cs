using System.Collections.Generic;
using Newtonsoft.Json;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IMaturedBlockDeposits
    {
        [JsonConverter(typeof(ConcreteConverter<List<Deposit>>))]
        IReadOnlyList<IDeposit> Deposits { get; set; }

        [JsonConverter(typeof(ConcreteConverter<MaturedBlockModel>))]
        IMaturedBlock Block { get; set; }
    }
}