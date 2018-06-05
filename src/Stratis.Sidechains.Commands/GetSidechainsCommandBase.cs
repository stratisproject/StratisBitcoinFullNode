using System;
using System.Collections.Generic;
using System.Management.Automation;
using Stratis.Sidechains.Features.BlockchainGeneration;

namespace Stratis.Sidechains.Commands
{
    public abstract class GetSidechainsCommandBase : PSCmdlet
    {
        /// <summary>
        /// <para type="description">The quantity of the gizmo.</para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 0)]
        public string SidechainName { get; set; } = string.Empty;
        protected abstract Dictionary<string, SidechainInfo> GetSidechains();
        protected override void ProcessRecord()
        {
            var sidechains = this.GetSidechains();

            if (!string.IsNullOrEmpty(SidechainName))
            {
                if (sidechains.ContainsKey(this.SidechainName))
                    this.WriteObject(sidechains[this.SidechainName]);
                else
                    throw new KeyNotFoundException("The specified sidechain name does not exist.");
            }
            else foreach (var sidechain in sidechains.Values) WriteObject(sidechain);
            
        }
    }
}
