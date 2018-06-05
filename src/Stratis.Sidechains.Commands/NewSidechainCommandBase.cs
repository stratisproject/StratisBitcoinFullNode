using System.Management.Automation;
using Newtonsoft.Json;
using Stratis.Sidechains.Features.BlockchainGeneration;

namespace Stratis.Sidechains.Commands
{   [Cmdlet(VerbsCommon.New, "Sidechain")]
    public abstract class NewSidechainCommandBase : PSCmdlet
    {
        protected readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Include,
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
        };

        [Parameter(Mandatory = true, Position = 0)]
        public string SidechainName { get; set; }
        [Parameter(Mandatory = true, Position = 1)]
        public string CoinName { get; set; }
        [Parameter(Mandatory = true, Position = 2)]
        public int CoinType { get; set; }
        [Parameter(Mandatory = true, Position = 3)]
        public NetworkInfoRequest MainNet { get; set; }
        [Parameter(Mandatory = true, Position = 4)]
        public NetworkInfoRequest TestNet { get; set; }
        [Parameter(Mandatory = true, Position = 5)]
        public NetworkInfoRequest RegTest { get; set; }
        
        protected abstract void SaveSidechain(SidechainInfo sidechainInfo);
        protected override void ProcessRecord()
        {
            var sidechainInfo = new SidechainInfo(this.SidechainName, this.CoinName, this.CoinType, this.MainNet, this.TestNet, this.RegTest);
            this.SaveSidechain(sidechainInfo);
        }

    }
}