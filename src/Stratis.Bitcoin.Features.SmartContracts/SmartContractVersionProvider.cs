using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractVersionProvider : IVersionProvider
    {
        public string GetVersion()
        {
            return "0.13.0";
        }
    }
}
