using System;
using System.Text;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.SmartContracts.Core.Receipts
{
    /// <summary>
    /// Holds data about a particular smart contract execution.
    /// In the future this will need to be generated deterministically and could be used to store logs / events.
    /// For now it's 'debugging sugar'.
    /// </summary>
    public class SmartContractReceipt
    {
        [JsonProperty]
        public byte[] TxHash { get; private set; }

        [JsonProperty]
        public ulong BlockHeight { get; private set; }

        [JsonProperty]
        public byte[] NewContractAddress { get; private set; }

        [JsonProperty]
        public ulong GasConsumed { get; private set; }

        [JsonProperty]
        public bool Successful { get; private set; }

        [JsonProperty]
        public string Exception { get; private set; }

        [JsonProperty()]
        public string Returned { get; private set; }

        public SmartContractReceipt() { }

        public SmartContractReceipt(
            uint256 txHash,
            ulong blockHeight,
            uint160 newContractAddress,
            ulong gasConsumed,
            bool successful,
            Exception exception,
            object returned)
        {
            this.TxHash = txHash.ToBytes(); // Can't be null as it's used as the key anyhow.
            this.BlockHeight = blockHeight;
            this.NewContractAddress = newContractAddress?.ToBytes();
            this.GasConsumed = gasConsumed;
            this.Successful = successful;
            this.Exception = exception?.ToString();
            this.Returned = returned?.ToString();
        }

        public byte[] ToBytes()
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this));
        }

        public static SmartContractReceipt FromBytes(byte[] bytes)
        {
            return JsonConvert.DeserializeObject<SmartContractReceipt>(Encoding.UTF8.GetString(bytes));
        }
    }
}
