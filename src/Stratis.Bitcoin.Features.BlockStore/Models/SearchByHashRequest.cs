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
        /// Indicates if the Transactions should be returned with all details, or simply be returned as an string[] with hashes (txids).
        /// </summary>
        public bool Verbose { get; set; }
    }
}
