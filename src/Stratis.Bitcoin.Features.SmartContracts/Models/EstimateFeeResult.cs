using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public class EstimateFeeResult
    {
        private EstimateFeeResult(Money fee)
        {
            this.Fee = fee;
        }

        private EstimateFeeResult(string error, string message)
        {
            this.Error = error;
            this.Message = message;
        }

        public Money Fee { get; }

        public string Error { get; }

        public string Message { get; }

        public static EstimateFeeResult Success(Money fee)
        {
            return new EstimateFeeResult(fee);
        }

        public static EstimateFeeResult Failure(string error, string message)
        {
            return new EstimateFeeResult(error, message);
        }
    }
}