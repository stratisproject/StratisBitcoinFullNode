using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Features.FederatedPeg.IntegrationTests.Utils
{
    public static class FederatedPegTestExtensions
    {
        private const string WalletName = "mywallet";
        private const string WalletPassword = "password";
        private const string WalletPassphrase = "passphrase";
        private const string WalletAccount = "account 0";

        public static void AppendToConfig(this CoreNode node, string configKeyValueItem)
        {
            using (StreamWriter sw = File.AppendText(node.Config))
            {
                sw.WriteLine(configKeyValueItem);
            }
        }

        public static string GetUnusedAddress(this CoreNode node)
        {
            return node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, WalletAccount)).Address;
        }

        /// <summary>
        /// Get balance of the local wallet.
        /// </summary>
        public static Money GetBalance(this CoreNode node)
        {
            IEnumerable<Bitcoin.Features.Wallet.UnspentOutputReference> spendableOutputs = node.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName);
            return spendableOutputs.Sum(x => x.Transaction.Amount);
        }

        /// <summary>
        /// Note this is only going to work on smart contract enabled (aka sidechain) nodes
        /// </summary>
        public static byte[] QueryContractCode(this CoreNode node, string address, Network network)
        {
            IStateRepositoryRoot stateRoot = node.FullNode.NodeService<IStateRepositoryRoot>();
            return stateRoot.GetCode(address.ToUint160(network));
        }

        public static IFullNodeBuilder UseTestFedPegBlockDefinition(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                foreach (IFeatureRegistration feature in features.FeatureRegistrations)
                {
                    feature.FeatureServices(services =>
                    {
                        ServiceDescriptor cht = services.FirstOrDefault(x => x.ServiceType == typeof(BlockDefinition));

                        services.Remove(cht);
                        services.AddSingleton<BlockDefinition, TestFederatedPegBlockDefinition>();
                    });
                }
            });

            return fullNodeBuilder;
        }
    }
}
