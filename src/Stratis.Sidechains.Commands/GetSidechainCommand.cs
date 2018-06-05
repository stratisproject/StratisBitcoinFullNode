using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Newtonsoft.Json;
using Stratis.Sidechains.Features.BlockchainGeneration;

namespace Stratis.Sidechains.Commands
{
    /// <summary>
    /// <para type="synopsis">This is the cmdlet synopsis.</para>
    /// <para type="description">This is part of the longer cmdlet description.</para>
    /// <para type="description">Also part of the longer cmdlet description.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Sidechains")]
    public class GetSidechainsCommand : GetSidechainsCommandBase
    {
        public string DataDir { get; set; }

        protected override void BeginProcessing()
        {
            this.DataDir = this.SessionState.PSVariable.GetValue("StratisNodeDir") as string;
            if (string.IsNullOrEmpty(this.DataDir))
                this.DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StratisNode");
        }

        protected override Dictionary<string, SidechainInfo> GetSidechains()
        {
            string filename = Path.Combine(this.DataDir, "sidechains.json");
            if (System.IO.File.Exists(filename) == false)
                return new Dictionary<string, SidechainInfo>();
            else
            {
                string json = File.ReadAllText(filename);
                var sidechains = JsonConvert.DeserializeObject<Dictionary<string, SidechainInfo>>(json);
                return sidechains;
            }
        }
    }
}