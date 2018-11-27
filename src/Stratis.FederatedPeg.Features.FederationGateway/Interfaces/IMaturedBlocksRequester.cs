using System.Threading.Tasks;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IMaturedBlocksRequester
    {
        /// <summary>
        /// Starts the requester.
        /// </summary>
        void Start();

        /// <summary>
        /// Gets more blocks from the counter node.
        /// </summary>
        /// <returns><c>True</c> if more blocks were found and <c>false</c> otherwise.</returns>
        Task<bool> GetMoreBlocksAsync();
    }
}