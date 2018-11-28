using System;
using System.Collections.Generic;
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

        private StringBuilder stringBuilder;

        private Dictionary<int, string> consoleColors;

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

            this.stringBuilder = new StringBuilder();

            SetFolderVariables();
            CopyStratisChainFiles();
            AddCommentedFederationDetails();
            SetFederationVariables();
            SetTimeoutVariables();
            SetConsoleColors();
            StartGatewayDs();
            StartChainsD();
            EnableWallets();

            this.output.WriteLine(this.stringBuilder.ToString());
        }

        private void StartChainsD()
        {
            this.stringBuilder.AppendLine("# MainchainUser");
            this.stringBuilder.AppendLine("cd $path_to_stratisd");
            this.stringBuilder.AppendLine($"start-process cmd -ArgumentList \"/k color {this.consoleColors[5]} && dotnet run -testnet -port=36178 -apiport=38221 -agentprefix=mainuser -datadir=$root_datadir\\MainchainUser\"");
            this.stringBuilder.AppendLine("timeout $interval_time");
            this.stringBuilder.AppendLine(Environment.NewLine);

            this.stringBuilder.AppendLine("# SidechainUser");
            this.stringBuilder.AppendLine("cd path_to_sidechaind");
            this.stringBuilder.AppendLine($"start-process cmd -ArgumentList \"/k color {this.consoleColors[4]} && dotnet run -port=26179 -apiport=38225 -agentprefix=sideuser -datadir=$root_datadir\\SidechainUser agentprefix=sc_user -addnode=127.0.0.1:36{GetPortNumberSuffix(this.chains[1], 0)} -addnode=127.0.0.1:36{GetPortNumberSuffix(this.chains[1], 1)} -addnode=127.0.0.1:36{GetPortNumberSuffix(this.chains[1], 2)}\"");
            this.stringBuilder.AppendLine("timeout $interval_time");
            this.stringBuilder.AppendLine(Environment.NewLine);
        }

        private void StartGatewayDs()
        {
            this.stringBuilder.AppendLine("cd $path_to_federationgatewayd");
            federationMemberIndexes.ForEach(i => {
                this.stringBuilder.AppendLine($"# Federation member {i} main and side");
                this.stringBuilder.AppendLine(
                    $"start-process cmd -ArgumentList \"/k color {this.consoleColors[i+1]} && dotnet run -mainchain -agentprefix=fed{i + 1}main -datadir=$root_datadir\\gateway{i + 1} -port=36{GetPortNumberSuffix(this.chains[0], i)} -apiport=38{GetPortNumberSuffix(this.chains[0], i)} -counterchainapiport=38{GetPortNumberSuffix(this.chains[1], i)} -federationips=$mainchain_federationips -redeemscript=\"\"$redeemscript\"\" -publickey=$gateway{i+1}_public_key\"");
                this.stringBuilder.AppendLine("timeout $long_interval_time");
                this.stringBuilder.AppendLine(
                    $"start-process cmd -ArgumentList \"/k color {this.consoleColors[i + 1]} && dotnet run -sidechain -agentprefix=fed{i + 1}side -datadir=$root_datadir\\gateway{i + 1} mine=1 mineaddress=$sidechain_multisig_address -port=36{GetPortNumberSuffix(this.chains[1], i)} -apiport=38{GetPortNumberSuffix(this.chains[1], i)} -counterchainapiport=38{GetPortNumberSuffix(this.chains[0], i)} -txindex=1 -federationips=$sidechain_federationips -redeemscript=\"\"$redeemscript\"\" -publickey=$gateway{i + 1}_public_key\"");
                this.stringBuilder.AppendLine("timeout $long_interval_time");
                this.stringBuilder.AppendLine(Environment.NewLine);
            });
        }

        private void CopyStratisChainFiles()
        {
            this.stringBuilder.AppendLine(@"If ((Test-Path $env:APPDATA\StratisNode\stratis\StratisTest) -And -Not (Test-Path $root_datadir\gateway1\stratis\StratisTest\blocks)) {");
            this.stringBuilder.AppendLine(@"    $destinations = ""$root_datadir\gateway1\stratis\StratisTest""");
            this.stringBuilder.AppendLine(@"        ""$root_datadir\gateway2\stratis\StratisTest"",");
            this.stringBuilder.AppendLine(@"        ""$root_datadir\gateway3\stratis\StratisTest"",");
            this.stringBuilder.AppendLine(@"        ""$root_datadir\MainchainUser\stratis\StratisTest""");
            this.stringBuilder.AppendLine(
                @"    $destinations | % { Copy - Item $env:APPDATA\StratisNode\stratis\StratisTest\blocks - Recurse - Destination $_}");
            this.stringBuilder.AppendLine(@"    $destinations | % { Copy - Item $env:APPDATA\StratisNode\stratis\StratisTest\chain - Recurse - Destination $_}");
            this.stringBuilder.AppendLine(@"    $destinations | % { Copy - Item $env:APPDATA\StratisNode\stratis\StratisTest\coinview - Recurse - Destination $_}");
            this.stringBuilder.AppendLine(@"    Copy - Item - Path $path_to_stratis_wallet_with_funds - Destination $root_datadir\MainchainUser\stratis\StratisTest");
            this.stringBuilder.AppendLine(@"}");
            this.stringBuilder.AppendLine(Environment.NewLine);
        }

        private void SetFolderVariables()
        {
            var rootDataDir = @"C:\Users\Matthieu\AppData\Roaming\StratisNode\federation";
            var fedGatewayDDir = @"C:\Users\Matthieu\source\repos\FederatedSidechains\src\Stratis.FederationGatewayD";
            var sidechainDDir = @"C:\Users\Matthieu\source\repos\FederatedSidechains\src\Stratis.SidechainD";
            var stratisDDir = @"C:\Users\Matthieu\source\repos\StratisBitcoinFullNode\src\Stratis.StratisD";
            var walletFile = @"C:\Users\Matthieu\AppData\Roaming\StratisNode\stratis\StratisTest\walletTest1.wallet.json";
            this.stringBuilder.AppendLine("###############################");
            this.stringBuilder.AppendLine("#    UPDATE THESE 5 VALUES    #");
            this.stringBuilder.AppendLine("###############################");
            this.stringBuilder.AppendLine($"$root_datadir = \"{rootDataDir}\"");
            this.stringBuilder.AppendLine($"$path_to_federationgatewayd = \"{fedGatewayDDir}\"");
            this.stringBuilder.AppendLine($"$path_to_sidechaind = \"{sidechainDDir}\"");
            this.stringBuilder.AppendLine($"$path_to_stratisd = \"{stratisDDir}\"");
            this.stringBuilder.AppendLine($"$path_to_stratis_wallet_with_funds = \"{walletFile}\"");
            this.stringBuilder.AppendLine(Environment.NewLine);
        }

        private void EnableWallets()
        {
            this.stringBuilder.AppendLine("######### API Queries to enable federation wallets ###########");
            this.chains.ForEach(c =>
            {
                this.stringBuilder.AppendLine($"# {c}");
                this.federationMemberIndexes.ForEach(i =>
                {
                    this.stringBuilder.AppendLine(
                        $"$params = @{{ \"mnemonic\" = \"{this.mnemonics[i]}\"; \"password\" = \"password\" }}");
                    this.stringBuilder.AppendLine(
                        $"Invoke-WebRequest -Uri http://localhost:38{GetPortNumberSuffix(c,i)}/api/FederationWallet/import-key -Method post -Body ($params|ConvertTo-Json) -ContentType \"application/json\"");
                    this.stringBuilder.AppendLine("timeout $interval_time");
                    this.stringBuilder.AppendLine($"$params = @{{ \"password\" = \"password\" }}");
                    this.stringBuilder.AppendLine(
                        $"Invoke-WebRequest -Uri http://localhost:38{GetPortNumberSuffix(c, i)}/api/FederationWallet/enable-federation -Method post -Body ($params|ConvertTo-Json) -ContentType \"application/json\"");
                    this.stringBuilder.AppendLine("timeout $interval_time");
                    this.stringBuilder.AppendLine(Environment.NewLine);
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
            this.stringBuilder.AppendLine("# The interval between starting the networks run, in seconds.");
            this.stringBuilder.AppendLine("$interval_time = 5");
            this.stringBuilder.AppendLine("$long_interval_time = 10");
            this.stringBuilder.AppendLine(Environment.NewLine);
        }

        private void SetFederationVariables()
        {
            var mainFederationIps = this.federationMemberIndexes.Select(i => $"127.0.0.1:361{i + 1:00}");
            var sideFederationIps = this.federationMemberIndexes.Select(i => $"127.0.0.1:362{i + 1:00}");
            this.stringBuilder.AppendLine($"$mainchain_federationips = \"{string.Join(",", mainFederationIps)}\"");
            this.stringBuilder.AppendLine($"$sidechain_federationips = \"{string.Join(",", sideFederationIps)}\"");
            this.stringBuilder.AppendLine($"$redeemscript = \"{this.scriptAndAddresses.payToMultiSig}\"");
            this.stringBuilder.AppendLine($"$sidechain_multisig_address = \"{this.scriptAndAddresses.sidechainMultisigAddress}\"");
            this.federationMemberIndexes.ForEach(
                i => { this.stringBuilder.AppendLine($"$gateway{i + 1}_public_key = \"{this.pubKeysByMnemonic[this.mnemonics[i]]}\""); });
            this.stringBuilder.AppendLine(Environment.NewLine);
        }

        private void AddCommentedFederationDetails()
        {
            this.stringBuilder.AppendLine("# FEDERATION DETAILS");
            this.federationMemberIndexes.ForEach(
                i =>
                    {
                        this.stringBuilder.AppendLine($"# Member{i + 1} mnemonic: {this.mnemonics[i]}");
                        this.stringBuilder.AppendLine($"# Member1 public key: {this.pubKeysByMnemonic[this.mnemonics[0]]}");
                    });

            this.stringBuilder.AppendLine($"# Redeem script: {this.scriptAndAddresses.payToMultiSig}");
            this.stringBuilder.AppendLine($"# Sidechan P2SH: {this.scriptAndAddresses.sidechainMultisigAddress.ScriptPubKey}");
            this.stringBuilder.AppendLine($"# Sidechain Multisig address: {this.scriptAndAddresses.sidechainMultisigAddress}");
            this.stringBuilder.AppendLine($"# Mainchain P2SH: {this.scriptAndAddresses.mainchainMultisigAddress.ScriptPubKey}");
            this.stringBuilder.AppendLine($"# Mainchain Multisig address: {this.scriptAndAddresses.mainchainMultisigAddress}");
            this.stringBuilder.AppendLine(Environment.NewLine);
        }

        private void SetConsoleColors()
        {
            this.stringBuilder.AppendLine("$console_colors = @{ ");
            this.stringBuilder.AppendLine("   1 = \"0E\"; # gateway 1 # light yellow on black");
            this.stringBuilder.AppendLine("   2 = \"0A\"; # gateway 2 # light green on black");
            this.stringBuilder.AppendLine("   3 = \"09\"; # gateway 3 # light blue on black");
            this.stringBuilder.AppendLine("   4 = \"0C\"; # miner     # light red on black");
            this.stringBuilder.AppendLine("   5 = \"0D\"; # wallets   # light purple on black");
            this.stringBuilder.AppendLine("}");
            this.consoleColors =
                new Dictionary<int, string>() { { 1, "0E" }, { 2, "0A" }, { 3, "09" }, { 4, "0C" }, { 5, "0D" }, };
            this.stringBuilder.AppendLine(Environment.NewLine);
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