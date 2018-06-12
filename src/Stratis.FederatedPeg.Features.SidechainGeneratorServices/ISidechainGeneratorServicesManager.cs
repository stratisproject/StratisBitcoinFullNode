namespace Stratis.FederatedPeg.Features.SidechainGeneratorServices
{
    /// <summary>
    /// This manages the two services required of the sidechain during the sidechain generation.
    /// It outputs the redeem script and multisig address to the federation folder
    /// and mines the first blocks while directing the resulting block reward into the multi-sig. 
    /// </summary>
    public interface ISidechainGeneratorServicesManager
    {
        /// <summary>
        /// Outputs the Sidechain multisig redeem script and the address of the multi-sig
        /// into the federation folder.
        /// </summary>
        /// <param name="multiSigM">The number of signatures required to form a quorum.</param>
        /// <param name="multiSigN">The number of members in the federation.</param>
        /// <param name="folder">The path to the federation folder on the local machine.</param>
        void OutputScriptPubKeyAndAddress(int multiSigN, int multiSigM, string folder);
    }
}
