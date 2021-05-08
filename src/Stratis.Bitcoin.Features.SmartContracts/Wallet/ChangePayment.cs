using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    public class ChangePayment
    {
        private readonly SpendingDetails spendingDetails;
        private readonly PaymentDetails paymentDetails;

        public ChangePayment(SpendingDetails spendingDetails, PaymentDetails paymentDetails)
        {
            this.spendingDetails = spendingDetails;
            this.paymentDetails = paymentDetails;
        }

        public bool IsChange(TransactionData output)
        {
            return this.spendingDetails.TransactionId == output.Id &&
                   this.paymentDetails.OutputIndex == output.Index;
        }
    }
}