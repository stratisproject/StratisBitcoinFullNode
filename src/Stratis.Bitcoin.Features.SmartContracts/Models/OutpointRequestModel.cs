using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;


namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    /// <summary>
    /// Class representing single transaction input.
    /// </summary>
    public class OutpointRequestModel
    {
        /// <summary>
        /// The transaction ID.
        /// </summary>
        [Required(ErrorMessage = "The transaction id is missing.")]
        public string TransactionId { get; set; }

        /// <summary>
        /// The index of the output in the transaction.
        /// </summary>
        [Required(ErrorMessage = "The index of the output in the transaction is missing.")]
        public int Index { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
