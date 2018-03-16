using System.ComponentModel.DataAnnotations;
using Stratis.SmartContracts.Core;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public class GetStorageRequest
    {
        [Required(ErrorMessage = "A contract address is required.")]
        public string ContractAddress { get; set; }

        [Required(ErrorMessage = "A storage key is required.")]
        public string StorageKey { get; set; }

        [Required(ErrorMessage = "A data type is required.")]
        public SmartContractCarrierDataType DataType { get; set; }
    }
}
