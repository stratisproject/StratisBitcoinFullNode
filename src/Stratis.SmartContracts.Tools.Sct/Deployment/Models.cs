using System.Collections.Generic;

namespace Stratis.SmartContracts.Tools.Sct.Deployment
{
    internal class BuildCreateContractTransactionRequest
    {
        public string WalletName { get; set; }        
        public string AccountName { get; set; }        
        public string Amount { get; set; }
        public string FeeAmount { get; set; }
        public string Password { get; set; }
        public string ContractCode { get; set; }
        public string GasPrice { get; set; }
        public string GasLimit { get; set; }
        public string Sender { get; set; }
        public string[] Parameters { get; set; }
    }

    internal class BuildCreateContractTransactionResponse
    {       
        [NetJSON.NetJSONProperty("newContractAddress")]
        public string NewContractAddress { get; set; }
    }

    internal class ErrorResponse
    {
        [NetJSON.NetJSONProperty("errors")]
        public List<ErrorModel> Errors { get; set; }
    }

    internal class ErrorModel
    {
        [NetJSON.NetJSONProperty("status")]
        public int Status { get; set; }

        [NetJSON.NetJSONProperty("message")]
        public string Message { get; set; }

        [NetJSON.NetJSONProperty("description")]
        public string Description { get; set; }
    }
}
