using Newtonsoft.Json;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;

namespace Stratis.Features.FederatedPeg.Models
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
