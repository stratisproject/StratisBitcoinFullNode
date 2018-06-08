using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Sidechains.Features.BlockchainGeneration;

namespace Stratis.FederatedPeg.Features.SidechainGeneratorServices
{
    ///<inheritdoc/>
    public class SidechainGeneratorServicesManager : ISidechainGeneratorServicesManager
    {
        // The network must be a sidechain network.
        private Network network;

        // The miner.
        private IPowMining powMining;

        /// <summary>
        /// Ensures the network is a sidechain and gets the required miner from the fullnode.
        /// </summary>
        /// <param name="network">The sidechain network used.</param>
        /// <param name="fullNode">The fullnode that supplies the miner.</param>
        public SidechainGeneratorServicesManager(Network network, FullNode fullNode)
        {
            // We must have a sidechain network.
            Guard.Assert(network.ToChain() == Chain.Sidechain);

            this.network = network;
            this.powMining = fullNode.NodeService<IPowMining>();
        }

        ///<inheritdoc/>
        public string GetSidechainName()
        {
            return SidechainIdentifier.Instance.Name;
        }

        ///<inheritdoc/>
        public void OutputScriptPubKeyAndAddress(int multiSigM, int multiSigN, string folder)
        {
            // Load the federation members from the specified folder.
            // Checks N matches member count also.
            var memberFolderManager = new MemberFolderManager(folder);
            var federation = memberFolderManager.LoadFederation(multiSigM, multiSigN);

            // Generate the ScriptPubKey and address files for the sidechain.
            memberFolderManager.OutputScriptPubKeyAndAddress(federation, this.network);
        }
    }
}