using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public class GetStorageRequest
    {
        [Required(ErrorMessage = "A contract address is required.")]
        public string ContractAddress { get; set; }

        [Required(ErrorMessage = "A storage key is required.")]
        public string StorageKey { get; set; }

        [Required(ErrorMessage = "A data type is required.")]
        public SmartContractDataType DataType { get; set; }
    }

    public enum SmartContractDataType
    {
        Bytes,
        Char,
        Address,
        Bool,
        Int,
        Long,
        Uint,
        Ulong,
        Sbyte,
        String
    }
}