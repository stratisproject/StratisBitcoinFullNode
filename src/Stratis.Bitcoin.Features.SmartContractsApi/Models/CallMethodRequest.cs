using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Features.SmartContractsApi.Models
{
    public class CallMethodRequest
    {
        [Required(ErrorMessage = "A contract address is required.")]
        public string ContractAddress { get; set; }

        [Required(ErrorMessage = "A method name is required.")]
        public string MethodName { get; set; }

        public string[] Parameters { get; set; }
    }
}
