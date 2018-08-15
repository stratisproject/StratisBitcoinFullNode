using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Features.BlockStore.Models
{
    public abstract class RequestBase
    {
        public bool OutputJson { get; set; }
    }

    public class SearchByHashRequest : RequestBase
    {
        [Required(AllowEmptyStrings = false)]
        public string Hash { get; set; }

        /// <summary>
        /// Indicates if the Transactions should be returned with all details, or simply be returned as an <see cref="string[]"/> with hashes (txids).
        /// </summary>
        /// <remarks>This is not considered when <see cref="RequestBase.OutputJson"/> is set to false.</remarks>
        public bool ShowTransactionDetails { get; set; }
    }
}
