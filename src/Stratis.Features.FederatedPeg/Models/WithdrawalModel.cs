using System;
using Newtonsoft.Json;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;

namespace Stratis.Features.FederatedPeg.Models
{
    public class WithdrawalModel
    {
        [JsonConverter(typeof(ConcreteConverter<Withdrawal>))]
        public IWithdrawal Withdrawal { get; set; }

        public string SpendingOutputDetails { get; set; }

        public DateTime TimeUTC { get; set; }

        public string TransferStatus { get; set; }

        public override string ToString()
        {
            return this.Withdrawal.GetInfo() + " Spending=" + this.SpendingOutputDetails + " Status=" + this.TransferStatus;
        }
    }
}
