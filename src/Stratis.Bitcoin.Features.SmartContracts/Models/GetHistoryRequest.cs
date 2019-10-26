namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public class GetHistoryRequest
    {
        public string WalletName { get; set; }

        public string Address { get; set; }

        /// <summary>
        /// An optional value allowing (with Take) pagination of the wallet's history. If given,
        /// the member specifies the numbers of records in the wallet's history to skip before
        /// beginning record retrieval; otherwise the wallet history records are retrieved starting from 0.
        /// </summary>      
        public int? Skip { get; set; }

        /// <summary>
        /// An optional value allowing (with Skip) pagination of the wallet's history. If given,
        /// the member specifies the number of records in the wallet's history to retrieve in this call; otherwise all
        /// wallet history records are retrieved.
        /// </summary>  
        public int? Take { get; set; }
    }
}
