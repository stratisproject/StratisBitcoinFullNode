using System.Linq;
using System.Collections.Generic;
using Stratis.Bitcoin.Controllers.Models;

namespace AddressOwnershipTool
{
    public class BlockExplorerClient
    {
        private const string ExplorerBaseUrl = "https://stratissnapshotapi.stratisplatform.com/";

        public bool HasBalance(string address)
        {
            var stratisApiClient = new NodeApiClient($"{ExplorerBaseUrl}api");
            var balance = stratisApiClient.GetAddressBalance(address);

            return balance > 0;
        }

        public bool HasActivity(string address)
        {
            var stratisApiClient = new NodeApiClient($"{ExplorerBaseUrl}api");
            List<AddressBalanceChange> changes = stratisApiClient.GetVerboseAddressBalance(address);

            return changes.Any();
        }
    }
}
