using System.ComponentModel.DataAnnotations;
using Stratis.SmartContracts.CLR.Serialization;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public class GetStorageRequest
    {
        [Required(ErrorMessage = "A contract address is required.")]
        public string ContractAddress { get; set; }

        [Required(ErrorMessage = "A storage key is required.")]
        public string StorageKey { get; set; }

        [Required(ErrorMessage = "A data type is required.")]
        public MethodParameterDataType DataType { get; set; }
    }
}