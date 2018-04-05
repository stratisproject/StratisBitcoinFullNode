using System.Threading.Tasks;

namespace Stratis.FederatedPeg.Features.MainchainGeneratorServices
{
    /// <summary>
    /// This class encapsulates the sidechain initialization process used by the Sidechain Generator. 
    /// </summary>
    public interface IMainchainGeneratorServicesManager
    {
        /// <summary>
        /// The initialization performs the following actions:
        /// 1.  Ensures we are communicating with the expected sidechain.
        /// 2.  Loads the federation and generates the redeem script and public addresses
        ///     for the multi-sigs on both chains.
        /// 3.  Mines the premine into the multi-sig address on the sidechain. 
        /// </summary>
        /// <param name="sidechainName">The name of the sidechain used to ensure we are communicating with the correct chain.</param>
        /// <param name="apiPortForSidechain">The port the sidechain API is running on.</param>
        /// <param name="multiSigN">The number of members in the federation.</param>
        /// <param name="multiSigM">The number of members required to reach a quorum for transaction signing.</param>
        /// <param name="folderFedMemberKey">The location of the Federation Folder that contains the public keys for all the federation members.</param>
        Task InitSidechain(string sidechainName, int apiPortForSidechain, int multiSigN, int multiSigM, string folderFedMemberKey);
    }
}