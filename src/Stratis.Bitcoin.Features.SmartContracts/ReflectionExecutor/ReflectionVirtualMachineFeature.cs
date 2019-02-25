using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder.Feature;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor
{
    public sealed class ReflectionVirtualMachineFeature : FullNodeFeature
    {
        private readonly ILogger logger;

        public ReflectionVirtualMachineFeature(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override Task InitializeAsync()
        {
            this.logger.LogInformation("Reflection Virtual Machine Injected.");
            
            return Task.CompletedTask;
        }
    }
}