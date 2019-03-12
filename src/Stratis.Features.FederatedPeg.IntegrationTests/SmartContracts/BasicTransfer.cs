using Stratis.SmartContracts;

namespace Stratis.Features.FederatedPeg.IntegrationTests.SmartContracts
{
    public class BasicTransfer : SmartContract
    {
        public BasicTransfer(ISmartContractState smartContractState) : base(smartContractState)
        {
        }

        public void SendToAddress(Address address)
        {
            Transfer(address, this.Balance);
        }
    }
}

