using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

namespace NBitcoin
{
    internal sealed class DefaultSidechainInfoProvider : ISidechainInfoProvider
    {
        private string filename;

        public DefaultSidechainInfoProvider()
        {
            //default folder is the AppData\StratisNode folder
            //2.0 version Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StratisNode");
            string appDataFolder = Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "AppData" : "Home");
            string folder = Path.Combine(appDataFolder, "StratisNode");
            string filename = Path.Combine(folder, "sidechains.json");
            this.VerifyFolder(filename);
        }

        //use an alternative folder
        public DefaultSidechainInfoProvider(string folder)
        {
            string filename = Path.Combine(folder, "sidechains.json");
            this.VerifyFolder(filename);
        }

        public void VerifyFolder(string filename)
        {
            if (!File.Exists(filename))
                throw new ArgumentException("Not a valid sidechain folder.  The folder must contain the sidechains.json file.");
            this.filename = filename;
        }

        public SidechainInfo GetSidechainInfo(string sidechainName)
        {
            var jsonSerializerSettings = new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Include,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
            };

            string json = File.ReadAllText(this.filename);

            var dictionary = JsonConvert.DeserializeObject<Dictionary<string, SidechainInfo>>(json);
            return dictionary[SidechainIdentifier.Instance.Name];
        }
    }
}
