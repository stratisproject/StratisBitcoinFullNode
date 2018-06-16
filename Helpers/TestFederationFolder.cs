//using System.IO;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using NBitcoin;
//using Stratis.Bitcoin.Features.Wallet;
//using Stratis.Bitcoin.Tests.Common;using Stratis.Bitcoin.IntegrationTests.Common;
//using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
//using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
//using Stratis.FederatedPeg.Features.FederationGateway.Wallet;

//namespace Stratis.FederatedPeg.IntegrationTests.Helpers
//{
//    internal class TestFederationFolder
//    {
//        //eg: Federations\deposit_funds_to_sidechain
//        public string Folder { get; }

//        public TestFederationFolder([CallerMemberName] string caller = null)
//        {
//            this.Folder = Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), $"Federations\\{caller}"));
//            Directory.CreateDirectory(this.Folder);
//            TestBase.AssureEmptyDir(this.Folder);
//        }

//        //public MemberFolderManager CreateMemberFolderManager()
//        //{
//        //    return null;
//        //}

//        public void RunFedKeyPairGen(string name, string password)
//        {
//            string destination = $"{this.Folder}\\{name}";
//            Directory.CreateDirectory(destination);
//            IntegrationTestUtils.RunFedKeyPairGen(name, password, destination);
//        }

//        public void DistributeKeys(string[] folderNames)
//        {
//            foreach (string folder in folderNames)
//            {
//                var source = new DirectoryInfo($"{this.Folder}\\{folder}");
//                var files = source.GetFiles("PUBLIC*");
//                foreach (var file in files)
//                {
//                    string dest = $"{file.Directory.Parent.FullName}\\{file.Name}";
//                    File.Copy(file.FullName, $"{file.Directory.Parent.FullName}\\{file.Name}");
//                }
//            }
//        }

//        public void DistributeScriptAndAddress(string[] folderNames)
//        {
//            foreach (string folder in folderNames)
//            {
//                string dest = Path.Combine(this.Folder, folder);
//                File.Copy( Path.Combine(this.Folder, "Mainchain_Address.txt"), Path.Combine(dest, "Mainchain_Address.txt"));
//                File.Copy( Path.Combine(this.Folder, "Sidechain_Address.txt"), Path.Combine(dest, "Sidechain_Address.txt"));
//                File.Copy( Path.Combine(this.Folder, "Mainchain_ScriptPubKey.txt"), Path.Combine(dest, "Mainchain_ScriptPubKey.txt"));
//                File.Copy( Path.Combine(this.Folder, "Sidechain_ScriptPubKey.txt"), Path.Combine(dest, "Sidechain_ScriptPubKey.txt"));
//            }
//        }

//        public void ImportPrivateKeyToWallet(CoreNode node, string walletName, string walletPassword, string memberName,
//            string memberPassword, int m, int n, Network network)
//        {
//            // Use the GeneralWalletManager and get the API created wallet.
//            var generalWalletManager = node.FullNode.NodeService<IFederationWalletManager>() as FederationWalletManager;
//            var wallet = generalWalletManager.GetWallet();

//            //Decrypt the private key
//            var chain = network.ToChain();
//            string privateKeyEncrypted = File.ReadAllText(Path.Combine(this.Folder, $"{memberName}\\PRIVATE_DO_NOT_SHARE_{chain}_{memberName}.txt"));
            
//            var privateKeyDecryptString = HdOperations.DecryptSeed(privateKeyEncrypted, memberPassword, network);

//            var multiSigAddress = new MultiSigAddress();
            
//            // TODO: Change this to generate the requisite ExtKeys and ExtPubKeys instead
//            //multiSigAddress.Create(privateKeyDecryptString, publicKeys, m, network);

//            //account.ImportMultiSigAddress(multiSigAddress);

//            generalWalletManager.SaveWallet();

//          //  return account;
//        }

//        public void SaveGeneralWallet(CoreNode node, string walletName)
//        {
//            var generalWalletManager = node.FullNode.NodeService<IFederationWalletManager>() as FederationWalletManager;
//            var wallet = generalWalletManager.GetWallet();
//            generalWalletManager.SaveWallet();
//        }
//    }
//}
