using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Features.FederatedPeg.Tests.Utils
{
    public static class FedPegTestsHelper
    {
        public static FederatedPegSettings CreateSettings(Network network, out NodeSettings nodeSettings)
        {
            string redeemScript = "2 02fad5f3c4fdf4c22e8be4cfda47882fff89aaa0a48c1ccad7fa80dc5fee9ccec3 02503f03243d41c141172465caca2f5cef7524f149e965483be7ce4e44107d7d35 03be943c3a31359cd8e67bedb7122a0898d2c204cf2d0119e923ded58c429ef97c 3 OP_CHECKMULTISIG";
            string federationIps = "127.0.0.1:36201,127.0.0.1:36202,127.0.0.1:36203";
            string multisigPubKey = "03be943c3a31359cd8e67bedb7122a0898d2c204cf2d0119e923ded58c429ef97c";
            string[] args = new[] { "-sidechain", "-regtest", $"-federationips={federationIps}", $"-redeemscript={redeemScript}", $"-publickey={multisigPubKey}", "-mincoinmaturity=1", "-mindepositconfirmations=1" };
            nodeSettings = new NodeSettings(network, ProtocolVersion.ALT_PROTOCOL_VERSION, args: args);

            var settings = new FederatedPegSettings(nodeSettings);

            return settings;
        }
    }
}
