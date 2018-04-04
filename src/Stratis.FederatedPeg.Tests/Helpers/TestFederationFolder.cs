using System.IO;
using System.Runtime.CompilerServices;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common;

namespace Stratis.FederatedPeg.IntegrationTests.Helpers
{
    internal class TestFederationFolder
    {
        //eg: Federations\deposit_funds_to_sidechain
        public string Folder { get; }

        public TestFederationFolder([CallerMemberName] string caller = null)
        {
            this.Folder = Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), $"Federations\\{caller}"));
            Directory.CreateDirectory(this.Folder);
            TestUtils.ShellCleanupFolder(this.Folder);
        }

        public MemberFolderManager CreateMemberFolderManager()
        {
            return new MemberFolderManager(this.Folder);
        }

        public void RunFedKeyPairGen(string name, string password)
        {
            string destination = $"{this.Folder}\\{name}";
            Directory.CreateDirectory(destination);
            IntegrationTestUtils.RunFedKeyPairGen(name, password, destination);
        }

        public void DistributeKeys(string[] folderNames)
        {
            foreach (string folder in folderNames)
            {
                var source = new DirectoryInfo($"{this.Folder}\\{folder}");
                var files = source.GetFiles("PUBLIC*");
                foreach (var file in files)
                {
                    string dest = $"{file.Directory.Parent.FullName}\\{file.Name}";
                    File.Copy(file.FullName, $"{file.Directory.Parent.FullName}\\{file.Name}");
                }
            }
        }

        public void DistributeScriptAndAddress(string[] folderNames)
        {
            foreach (string folder in folderNames)
            {
                string dest = Path.Combine(this.Folder, folder);
                File.Copy( Path.Combine(this.Folder, "Mainchain_Address.txt"), Path.Combine(dest, "Mainchain_Address.txt"));
                File.Copy( Path.Combine(this.Folder, "Sidechain_Address.txt"), Path.Combine(dest, "Sidechain_Address.txt"));
                File.Copy( Path.Combine(this.Folder, "Mainchain_ScriptPubKey.txt"), Path.Combine(dest, "Mainchain_ScriptPubKey.txt"));
                File.Copy( Path.Combine(this.Folder, "Sidechain_ScriptPubKey.txt"), Path.Combine(dest, "Sidechain_ScriptPubKey.txt"));
            }
        }
    }
}
