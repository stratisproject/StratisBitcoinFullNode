using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor
{
    public sealed class ReflectionVirtualMachineFeature : FullNodeFeature
    {
        private readonly ILogger logger;
        private readonly Network network;
        private readonly ICallDataSerializer callDataSerializer;

        public ReflectionVirtualMachineFeature(ILoggerFactory loggerFactory, Network network, ICallDataSerializer callDataSerializer)
        {
            this.network = network;
            this.callDataSerializer = callDataSerializer;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override Task InitializeAsync()
        {
            this.RegisterRules(this.network.Consensus);

            this.logger.LogInformation("Reflection Virtual Machine Injected.");
            
            return Task.CompletedTask;
        }

        private void RegisterRules(IConsensus consensus)
        {
            consensus.FullValidationRules = new List<IFullValidationConsensusRule>()
            {
                new SmartContractFormatRule(this.callDataSerializer)
            };
        }
    }
}