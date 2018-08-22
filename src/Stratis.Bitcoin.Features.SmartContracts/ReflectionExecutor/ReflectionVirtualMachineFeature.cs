using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Builder.Feature;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor
{
    public sealed class ReflectionVirtualMachineFeature : FullNodeFeature
    {
        private readonly ILogger logger;
        private readonly Network network;

        public ReflectionVirtualMachineFeature(ILoggerFactory loggerFactory, Network network)
        {
            this.network = network;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override void Initialize()
        {
            new ReflectionRuleRegistration().RegisterRules(this.network.Consensus);

            this.logger.LogInformation("Reflection Virtual Machine Injected.");
        }
    }
}