using System;

namespace AddressOwnershipTool
{
    public class BlockExplorerClient
    {
        private const string ExplorerBaseUrl = "https://stratissnapshotapi.stratisplatform.com/";

        public bool HasBalance(string address)
        {
            var stratisApiClient = new NodeApiClient($"{ExplorerBaseUrl}api");
            var balance = stratisApiClient.GetAddressBalance(address);

            Console.WriteLine($"Balance for {address} is {(balance / 100_000_000):N8}");

            return balance > 0;
        }
    }
}
