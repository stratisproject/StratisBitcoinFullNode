using System;
using System.Collections.Generic;
using System.Text;

namespace City.Features.BlockExplorer.Models
{
    public class BlockQueryRequest : RequestBase
    {
        /// <summary>
        /// Indicates if the Transactions should be returned with all details, or simply be returned as an <see cref="string[]"/> with hashes (txids).
        /// </summary>
        /// <remarks>This is not considered when <see cref="RequestBase.OutputJson"/> is set to false.</remarks>
        public bool ShowTransactionDetails { get; set; }
    }
}
