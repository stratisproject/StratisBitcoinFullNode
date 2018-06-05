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
    [Cmdlet(VerbsCommon.New, "Sidechain")]
    public class NewSidechainCommand : NewSidechainCommandBase
    {
        public string DataDir { get; set; }
        
        protected override void BeginProcessing()
        {
            this.DataDir = this.SessionState.PSVariable.GetValue("StratisNodeDir") as string;
            if (string.IsNullOrEmpty(this.DataDir))
                this.DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StratisNode");
        }

        protected override void SaveSidechain(SidechainInfo sidechainInfo)
        {
            var dictionaryOut = this.GetSidechains();
            if (dictionaryOut.ContainsKey(sidechainInfo.ChainName))
                throw new ArgumentException($"A sidechain with the name ${sidechainInfo.ChainName} already exists.");

            dictionaryOut.Add(sidechainInfo.ChainName, sidechainInfo);
            this.SaveSidechains(dictionaryOut);
        }

        private void SaveSidechains(Dictionary<string, SidechainInfo> dictionary)
        {
            string filename = Path.Combine(this.DataDir, "sidechains.json");

            string json = JsonConvert.SerializeObject(dictionary, Formatting.Indented, this.jsonSerializerSettings);
            File.WriteAllText(filename, json);
        }

        private Dictionary<string, SidechainInfo> GetSidechains()
        {
            string filename = Path.Combine(this.DataDir, "sidechains.json");
            if (System.IO.File.Exists(filename) == false)
                return new Dictionary<string, SidechainInfo>();
            else
            {
                string json = File.ReadAllText(filename);
                return JsonConvert.DeserializeObject<Dictionary<string, SidechainInfo>>(json);
            }
        }
    }
}