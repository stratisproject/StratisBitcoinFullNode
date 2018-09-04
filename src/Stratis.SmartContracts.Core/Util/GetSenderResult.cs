using NBitcoin;

namespace Stratis.SmartContracts.Core.Util
{
    public class GetSenderResult
    {
        private GetSenderResult(uint160 address)
        {
            this.Success = true;
            this.Sender = address;
        }

        private GetSenderResult(string error)
        {
            this.Success = false;
            this.Error = error;
        }

        public bool Success { get; }

        public uint160 Sender { get; }

        public string Error { get; }

        public static GetSenderResult CreateSuccess(uint160 address)
        {
            return new GetSenderResult(address);
        }

        public static GetSenderResult CreateFailure(string error)
        {
            return new GetSenderResult(error);
        }
    }
}
