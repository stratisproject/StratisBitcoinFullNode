using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Features.BlockStore.Models
{
    public abstract class RequestBase
    {
        public bool OutputJson { get; set; }
    }

    /// <summary>
    /// A class containing the necessary parameters for a block search request.
    /// </summary>
    public class SearchByHashRequest : RequestBase
    {
        /// <summary>
        /// The hash of the required block.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string Hash { get; set; }

        /// <summary>
        /// A flag that indicates whether to return each block transaction complete with details
        /// or simply return transaction hashes (TX IDs).
        /// </summary>
        /// <remarks>This flag is not used when <see cref="RequestBase.OutputJson"/> is set to false.</remarks>
        public bool ShowTransactionDetails { get; set; }
    }
}
