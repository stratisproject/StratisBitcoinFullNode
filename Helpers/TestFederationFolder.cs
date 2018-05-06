using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Features.GeneralPurposeWallet;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Interfaces;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common.EnvironmentMockUp;

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

        public GeneralPurposeAccount ImportPrivateKeyToWallet(CoreNode node, string walletName, string walletPassword, string memberName,
            string memberPassword, int m, int n, Network network)
        {
            // Use the GeneralWalletManager and get the API created wallet.
            var generalWalletManager = node.FullNode.NodeService<IGeneralPurposeWalletManager>() as GeneralPurposeWalletManager;
            var wallet = generalWalletManager.GetWallet(walletName);

            // Use the first account.
            var account = wallet.GetAccountsByCoinType((Stratis.Bitcoin.Features.GeneralPurposeWallet.CoinType)node.FullNode.Network.Consensus.CoinType).First();

            //Decrypt the private key
            var chain = network.ToChain();
            string privateKeyEncrypted = File.ReadAllText(Path.Combine(this.Folder, $"{memberName}\\PRIVATE_DO_NOT_SHARE_{chain}_{memberName}.txt"));
            var privateKeyDecryptString = EncryptionProvider.DecryptString(privateKeyEncrypted, memberPassword);

            var multiSigAddress = new MultiSigAddress();

            var memberFolderManager = new MemberFolderManager(this.Folder);
            var federation = memberFolderManager.LoadFederation(m, n);
           
            var publicKeys = chain == Chain.Mainchain ?
                  (from f in federation.Members orderby f.PublicKeyMainChain.ToHex() select f.PublicKeyMainChain).ToArray()
                : (from f in federation.Members orderby f.PublicKeySideChain.ToHex() select f.PublicKeySideChain).ToArray();
            multiSigAddress.Create(new Key(Encoders.Hex.DecodeData(privateKeyDecryptString)), publicKeys, m, network);

            account.ImportMultiSigAddress(multiSigAddress);

            generalWalletManager.SaveWallet(wallet);

            return account;
        }

        public void SaveGeneralWallet(CoreNode node, string walletName)
        {
            var generalWalletManager = node.FullNode.NodeService<IGeneralPurposeWalletManager>() as GeneralPurposeWalletManager;
            var wallet = generalWalletManager.GetWallet(walletName);
            generalWalletManager.SaveWallet(wallet);
        }
    }
}
