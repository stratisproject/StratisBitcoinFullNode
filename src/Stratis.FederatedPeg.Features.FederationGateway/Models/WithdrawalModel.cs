using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;

namespace Stratis.FederatedPeg.Features.FederationGateway.Models
{
    public class WithdrawalModel
    {
        [JsonConverter(typeof(ConcreteConverter<Withdrawal>))]
        public IWithdrawal withdrawal { get; set; }

        public string TransferStatus { get; set; }

        public override string ToString()
        {
            return this.withdrawal.GetInfo() + " Status=" + this.TransferStatus;
        }
    }
}
