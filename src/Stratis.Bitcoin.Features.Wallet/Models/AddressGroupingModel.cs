using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public sealed class AddressGroupingModel
    {
        public string Address { get; set; }
        public Money Amount { get; set; }
    }
}
