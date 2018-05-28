using System.Text;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.SmartContracts.Core.Backend;

namespace Stratis.SmartContracts.Core.Receipts
{
    public class SmartContractReceipt
    {
        [JsonProperty]
        public byte[] TxHash { get; private set; }

        [JsonProperty]
        public ulong BlockHeight { get; private set; }

        [JsonProperty]
        public byte[] ContractAddress { get; private set; }

        [JsonProperty]
        public bool Successful { get; private set; }

        [JsonProperty]
        public string Exception { get; private set; }

        [JsonProperty()]
        public string Returned { get; private set; }

        public SmartContractReceipt() { }

        public SmartContractReceipt(uint256 txHash, ulong blockHeight, ISmartContractExecutionResult executionResult, uint160 contractAddress)
        {
            this.TxHash = txHash.ToBytes();
            this.BlockHeight = blockHeight;
            this.ContractAddress = (contractAddress != null) ? contractAddress.ToBytes() : executionResult.NewContractAddress.ToBytes();
            this.Successful = !executionResult.Revert;
            if (executionResult.Exception != null)
                this.Exception = executionResult.Exception.Message + executionResult.Exception.StackTrace;
            this.Returned = executionResult.Return?.ToString();
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
