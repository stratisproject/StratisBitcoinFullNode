using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using NBitcoin;

using Stratis.Bitcoin.Networks;
using Stratis.Sidechains.Networks;

using Xunit;
using Xunit.Abstractions;

namespace Stratis.FederatedPeg.Tests.Utils
{
    public class PowerShellScriptGeneratorAsTests
    {
        private readonly ITestOutputHelper output;

        private Network mainchainNetwork;

        private FederatedPegTest sidechainNetwork;

        private IList<Mnemonic> mnemonics;

        private Dictionary<Mnemonic, PubKey> pubKeysByMnemonic;

        private (Script payToMultiSig, BitcoinAddress sidechainMultisigAddress, BitcoinAddress mainchainMultisigAddress) scriptAndAddresses;

        private List<int> federationMemberIndexes;

        private List<string> chains;

        private Dictionary<int, string> consoleColors;

        private Action<string> newLine;

        public PowerShellScriptGeneratorAsTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void Generate_PS1_Fragment()
        {
            this.mainchainNetwork = Networks.Stratis.Testnet();
            this.sidechainNetwork = (FederatedPegTest)FederatedPegNetwork.NetworksSelector.Testnet();

            this.mnemonics = this.sidechainNetwork.FederationMnemonics;
            this.pubKeysByMnemonic = this.mnemonics.ToDictionary(m => m, m => m.DeriveExtKey().PrivateKey.PubKey);

            this.scriptAndAddresses = GenerateScriptAndAddresses(this.mainchainNetwork, this.sidechainNetwork, 2, this.pubKeysByMnemonic);

            this.federationMemberIndexes = Enumerable.Range(0, this.pubKeysByMnemonic.Count).ToList();
            this.chains = new[] { "mainchain", "sidechain" }.ToList();

            var stringBuilder = new StringBuilder();
            this.newLine = s => stringBuilder.AppendLine(s);

            SetFolderVariables();
            CopyStratisChainFiles();
            CreatePoaKeyFiles();
            AddCommentedFederationDetails();
            SetFederationVariables();
            SetTimeoutVariables();
            SetConsoleColors();
            StartGatewayDs();
            StartChainsD();
            EnableWallets();

            this.output.WriteLine(stringBuilder.ToString());
        }

        private void CreatePoaKeyFiles()
        {
            //for now just use the same private keys for multisig wallet and block signing
            this.federationMemberIndexes.ForEach(i =>
                {
                    var privateKey = this.mnemonics[i].DeriveExtKey().PrivateKey;
                    var targetFile = $"$root_datadir\\gateway{i+1}\\poa\\FederatedPegTest\\federationKey.dat";

                    var keyAsString = System.BitConverter.ToString(privateKey.ToBytes());
                    this.newLine($"$mining_key_hex_{i + 1} = \"{keyAsString}\"");
                    this.newLine($"$bytes_{i + 1} = foreach($hexByte in $mining_key_hex_{i + 1}.Split(\"-\")) {{[System.Convert]::ToByte($hexByte, 16)}}");
                    this.newLine($"New-Item -path \"{targetFile}\" -type file");
                    this.newLine($"$bytes_{i + 1} | set-content {targetFile} -Encoding Byte");
                });
            this.newLine(Environment.NewLine);
        }

        private void StartChainsD()
        {
            this.newLine("# MainchainUser");
            this.newLine("cd $path_to_stratisd");
            this.newLine($"start-process cmd -ArgumentList \"/k color {this.consoleColors[5]} && dotnet run --no-build -testnet -port=36178 -apiport=38221 -agentprefix=mainuser -datadir=$root_datadir\\MainchainUser -addnode=13.70.81.5 -addnode=52.151.76.252 -whitelist=52.151.76.252 -gateway=1\"");
            this.newLine("timeout $interval_time");
            this.newLine(Environment.NewLine);

            this.newLine("# SidechainUser");
            this.newLine("cd $path_to_sidechaind");
            this.newLine($"start-process cmd -ArgumentList \"/k color {this.consoleColors[4]} && dotnet run --no-build -testnet -port=26179 -apiport=38225 -agentprefix=sideuser -datadir=$root_datadir\\SidechainUser agentprefix=sc_user -addnode=127.0.0.1:36{GetPortNumberSuffix(this.chains[1], 0)} -addnode=127.0.0.1:36{GetPortNumberSuffix(this.chains[1], 1)} -addnode=127.0.0.1:36{GetPortNumberSuffix(this.chains[1], 2)}\"");
            this.newLine("timeout $interval_time");
            this.newLine(Environment.NewLine);
        }

        private void StartGatewayDs()
        {
            this.newLine("cd $path_to_federationgatewayd");
            federationMemberIndexes.ForEach(i => {
                this.newLine($"# Federation member {i} main and side");
                this.newLine(
                    $"start-process cmd -ArgumentList \"/k color {this.consoleColors[i+1]} && dotnet run --no-build -mainchain -testnet -agentprefix=fed{i + 1}main -datadir=$root_datadir\\gateway{i + 1} -port=36{GetPortNumberSuffix(this.chains[0], i)} -apiport=38{GetPortNumberSuffix(this.chains[0], i)} -counterchainapiport=38{GetPortNumberSuffix(this.chains[1], i)} -federationips=$mainchain_federationips -redeemscript=\"\"$redeemscript\"\" -publickey=$gateway{i+1}_public_key -addnode=13.70.81.5 -addnode=52.151.76.252 -whitelist=52.151.76.252 -gateway=1 -mincoinmaturity=1 -mindepositconfirmations=1\"");
                this.newLine("timeout $long_interval_time");
                this.newLine(
                    $"start-process cmd -ArgumentList \"/k color {this.consoleColors[i + 1]} && dotnet run --no-build -sidechain -testnet -agentprefix=fed{i + 1}side -datadir=$root_datadir\\gateway{i + 1} mine=1 mineaddress=$sidechain_multisig_address -port=36{GetPortNumberSuffix(this.chains[1], i)} -apiport=38{GetPortNumberSuffix(this.chains[1], i)} -counterchainapiport=38{GetPortNumberSuffix(this.chains[0], i)} -txindex=1 -federationips=$sidechain_federationips -redeemscript=\"\"$redeemscript\"\" -publickey=$gateway{i + 1}_public_key -mincoinmaturity=1 -mindepositconfirmations=1\"");
                this.newLine("timeout $long_interval_time");
                this.newLine(Environment.NewLine);
            });
        }

        private void CopyStratisChainFiles()
        {            
            // Create the folders in case they don't exist.
            this.newLine("# Create the folders in case they don't exist.");
            this.newLine("New-Item -ItemType directory -Force -Path $root_datadir");
            this.newLine(@"New-Item -ItemType directory -Force -Path $root_datadir\gateway1\stratis\StratisTest");
            this.newLine(@"New-Item -ItemType directory -Force -Path $root_datadir\gateway2\stratis\StratisTest");
            this.newLine(@"New-Item -ItemType directory -Force -Path $root_datadir\gateway3\stratis\StratisTest");
            this.newLine(@"New-Item -ItemType directory -Force -Path $root_datadir\MainchainUser\stratis\StratisTest");
            this.newLine(@"New-Item -ItemType directory -Force -Path $root_datadir\gateway1\poa\FederatedPegTest");
            this.newLine(@"New-Item -ItemType directory -Force -Path $root_datadir\gateway2\poa\FederatedPegTest");
            this.newLine(@"New-Item -ItemType directory -Force -Path $root_datadir\gateway3\poa\FederatedPegTest");
            this.newLine(Environment.NewLine);
            
            // Copy the blockchain data from a current, ideally up-to-date, Stratis Testnet folder.
            this.newLine("# Copy the blockchain data from a current, ideally up-to-date, Stratis Testnet folder.");
            this.newLine(@"If ((Test-Path $env:APPDATA\StratisNode\stratis\StratisTest) -And -Not (Test-Path $root_datadir\gateway1\stratis\StratisTest\blocks)) {");
            this.newLine(@"    $destinations = ""$root_datadir\gateway1\stratis\StratisTest"",");
            this.newLine(@"        ""$root_datadir\gateway2\stratis\StratisTest"",");
            this.newLine(@"        ""$root_datadir\gateway3\stratis\StratisTest"",");
            this.newLine(@"        ""$root_datadir\MainchainUser\stratis\StratisTest""");
            this.newLine(@"    $destinations | % { Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\blocks -Recurse -Destination $_}");
            this.newLine(@"    $destinations | % { Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\chain -Recurse -Destination $_}");
            this.newLine(@"    $destinations | % { Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\coinview -Recurse -Destination $_}");
            this.newLine(@"    $destinations | % { Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\finalizedBlock -Recurse -Destination $_}");
            this.newLine(@"    $destinations | % { Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\provenheaders -Recurse -Destination $_}");
            this.newLine(@"    Copy-Item -Path $path_to_stratis_wallet_with_funds -Destination $root_datadir\MainchainUser\stratis\StratisTest");
            this.newLine(@"}");
            this.newLine(Environment.NewLine);
        }

        private void SetFolderVariables()
        {
            var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var rootDataDir = Path.Combine(appDataDir, "StratisNode", "federation");
            var fedGatewayDDir = Path.Combine("$git_repos_path", "FederatedSidechains","src","Stratis.FederationGatewayD");
            var sidechainDDir = Path.Combine("$git_repos_path", "FederatedSidechains", "src", "Stratis.SidechainD");
            var stratisDDir = Path.Combine("$git_repos_path", "StratisBitcoinFullNode","src","Stratis.StratisD");
            var walletFile = Path.Combine(appDataDir, "StratisNode","stratis", this.mainchainNetwork.Name,"walletTest1.wallet.json");
            this.newLine("###############################");
            this.newLine("#    UPDATE THESE 5 VALUES    #");
            this.newLine("###############################");
            this.newLine($"$git_repos_path = \"{Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), "source", "repos")}\"");
            this.newLine($"$root_datadir = \"{rootDataDir}\"");
            this.newLine($"$path_to_federationgatewayd = \"{fedGatewayDDir}\"");
            this.newLine($"$path_to_sidechaind = \"{sidechainDDir}\"");
            this.newLine($"$path_to_stratisd = \"{stratisDDir}\"");
            this.newLine($"$path_to_stratis_wallet_with_funds = \"{walletFile}\"");
            this.newLine(Environment.NewLine);
        }

        private void EnableWallets()
        {
            this.newLine("######### API Queries to enable federation wallets ###########");
            this.chains.ForEach(c =>
            {
                this.newLine($"# {c}");
                this.federationMemberIndexes.ForEach(i =>
                {
                    this.newLine(
                        $"$params = @{{ \"mnemonic\" = \"{this.mnemonics[i]}\"; \"password\" = \"password\" }}");
                    this.newLine(
                        $"Invoke-WebRequest -Uri http://localhost:38{GetPortNumberSuffix(c,i)}/api/FederationWallet/import-key -Method post -Body ($params|ConvertTo-Json) -ContentType \"application/json\"");
                    this.newLine("timeout $interval_time");
                    this.newLine($"$params = @{{ \"password\" = \"password\" }}");
                    this.newLine(
                        $"Invoke-WebRequest -Uri http://localhost:38{GetPortNumberSuffix(c, i)}/api/FederationWallet/enable-federation -Method post -Body ($params|ConvertTo-Json) -ContentType \"application/json\"");
                    this.newLine("timeout $long_interval_time");
                    this.newLine(Environment.NewLine);
                });
            });
        }

        private string GetPortNumberSuffix(string chain, int memberIndex)
        {
            var chainIndex = chain == "mainchain" ? 1 : 2;
            return $"{chainIndex}{memberIndex + 1:00}";
        }

        private void SetTimeoutVariables()
        {
            this.newLine("# The interval between starting the networks run, in seconds.");
            this.newLine("$interval_time = 5");
            this.newLine("$long_interval_time = 10");
            this.newLine(Environment.NewLine);
        }

        private void SetFederationVariables()
        {
            var mainFederationIps = this.federationMemberIndexes.Select(i => $"127.0.0.1:361{i + 1:00}");
            var sideFederationIps = this.federationMemberIndexes.Select(i => $"127.0.0.1:362{i + 1:00}");
            this.newLine($"$mainchain_federationips = \"{string.Join(",", mainFederationIps)}\"");
            this.newLine($"$sidechain_federationips = \"{string.Join(",", sideFederationIps)}\"");
            this.newLine($"$redeemscript = \"{this.scriptAndAddresses.payToMultiSig}\"");
            this.newLine($"$sidechain_multisig_address = \"{this.scriptAndAddresses.sidechainMultisigAddress}\"");
            this.federationMemberIndexes.ForEach(
                i => { this.newLine($"$gateway{i + 1}_public_key = \"{this.pubKeysByMnemonic[this.mnemonics[i]]}\""); });
            this.newLine(Environment.NewLine);
        }

        private void AddCommentedFederationDetails()
        {
            this.newLine("# FEDERATION DETAILS");
            this.federationMemberIndexes.ForEach(
                i =>
                    {
                        this.newLine($"# Member{i + 1} mnemonic: {this.mnemonics[i]}");
                        this.newLine($"# Member{i + 1} public key: {this.pubKeysByMnemonic[this.mnemonics[i]]}");
                    });

            this.newLine($"# Redeem script: {this.scriptAndAddresses.payToMultiSig}");
            this.newLine($"# Sidechan P2SH: {this.scriptAndAddresses.sidechainMultisigAddress.ScriptPubKey}");
            this.newLine($"# Sidechain Multisig address: {this.scriptAndAddresses.sidechainMultisigAddress}");
            this.newLine($"# Mainchain P2SH: {this.scriptAndAddresses.mainchainMultisigAddress.ScriptPubKey}");
            this.newLine($"# Mainchain Multisig address: {this.scriptAndAddresses.mainchainMultisigAddress}");
            this.newLine(Environment.NewLine);
        }

        private void SetConsoleColors()
        {
            this.newLine("$console_colors = @{ ");
            this.newLine("   1 = \"0E\"; # gateway 1 # light yellow on black");
            this.newLine("   2 = \"0A\"; # gateway 2 # light green on black");
            this.newLine("   3 = \"09\"; # gateway 3 # light blue on black");
            this.newLine("   4 = \"0C\"; # miner     # light red on black");
            this.newLine("   5 = \"0D\"; # wallets   # light purple on black");
            this.newLine("}");
            this.consoleColors =
                new Dictionary<int, string>() { { 1, "0E" }, { 2, "0A" }, { 3, "09" }, { 4, "0C" }, { 5, "0D" }, };
            this.newLine(Environment.NewLine);
        }

        private IList<Mnemonic> GenerateMnemonics(int keyCount)
        {
            return Enumerable.Range(0, keyCount)
                .Select(k => new Mnemonic(Wordlist.English, WordCount.Twelve))
                .ToList();
        }


        private (Script payToMultiSig, BitcoinAddress sidechainMultisigAddress, BitcoinAddress mainchainMultisigAddress)
            GenerateScriptAndAddresses(Network mainchainNetwork, Network sidechainNetwork, int quorum, Dictionary<Mnemonic, PubKey> pubKeysByMnemonic)
        {
            Script payToMultiSig = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(quorum, pubKeysByMnemonic.Values.ToArray());
            BitcoinAddress sidechainMultisigAddress = payToMultiSig.Hash.GetAddress(sidechainNetwork);
            BitcoinAddress mainchainMultisigAddress = payToMultiSig.Hash.GetAddress(mainchainNetwork);
            return (payToMultiSig, sidechainMultisigAddress, mainchainMultisigAddress);
        }
    }
}
