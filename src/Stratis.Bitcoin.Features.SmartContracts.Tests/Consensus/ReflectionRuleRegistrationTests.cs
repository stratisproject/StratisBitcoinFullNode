using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus
{
    public sealed class ReflectionRuleRegistrationTests
    {
        [Fact]
        public void ReflectionVirtualMachineFeature_OnInitialize_RulesAdded()
        {
            Network network = KnownNetworks.StratisRegTest;
            var loggerFactory = new ExtendedLoggerFactory();

            var feature = new ReflectionVirtualMachineFeature(loggerFactory, network);
            feature.Initialize();

            Assert.Single(network.Consensus.Rules.Where(r => r.GetType() == typeof(SmartContractFormatRule)));
        }
    }
}