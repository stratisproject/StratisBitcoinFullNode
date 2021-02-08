using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using NBitcoin;
using Newtonsoft.Json;

namespace AddressOwnershipTool
{
    public class BlockExplorerClient
    {
        private const string ExplorerBaseUrl = "http://stratissnapshotapi.stratisplatrform.com/";

        public bool HasBalance(string address)
        {
            var stratisApiClient = new NodeApiClient($"{ExplorerBaseUrl}api");
            var balance = stratisApiClient.GetAddressBalance(address);

            Console.WriteLine($"Balance for {address} is {(balance / 100_000_000):N8}");

            return balance > 0;
        }
    }

    public class Balance
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("balance")]
        public ulong Amount { get; set; }
    }

    public class ApiResponse
    {
        [JsonProperty("balances")]
        public IList<Balance> Balances { get; set; }

        [JsonProperty("reason")]
        public object Reason { get; set; }
    }

}
