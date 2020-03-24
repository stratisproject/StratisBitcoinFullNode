using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public sealed class SmartContractVersionProvider : IVersionProvider
    {
        /// <inheritdoc />
        public string GetVersion()
        {
            return "3.0.8.0";
        }
    }
}
