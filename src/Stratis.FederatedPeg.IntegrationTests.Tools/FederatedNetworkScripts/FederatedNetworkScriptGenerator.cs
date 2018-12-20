using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Networks;
using Stratis.Sidechains.Networks;

namespace Stratis.FederatedPeg.IntegrationTests.Tools.FederatedNetworkScripts
{
    public class FederatedNetworkScriptGenerator : FederatedNetworkScriptBase<StratisTest, FederatedPegRegTest>
    {
        public FederatedNetworkScriptGenerator() : base((StratisTest)Networks.Stratis.Testnet(), (FederatedPegRegTest)FederatedPegNetwork.NetworksSelector.Regtest())
        {
            this.Initialize(
                mnemonics: this.sidechainNetwork.FederationMnemonics,
                federationMembersCount: this.sidechainNetwork.FederationMnemonics.Count
                );
        }

        protected override void BuildScript()
        {
            SetupNodes();
            SetFolderVariables();
            CreateHelpers();
            CopyStratisChainFiles();
            CreatePoaKeyFiles();
            AddCommentedFederationDetails();
            SetTimeoutVariables();
            StartGatewayDs();
            StartChainsD();
            CreateDebuggingDashboard();
            EnableWallets();
        }

        private NodeSetup DefaultGatewayConfigurator(NodeSetup nodeSetup, int nodeIndex)
        {
            NodeType primaryNodeType = nodeSetup.NodeType;
            NodeType counterNodeType = primaryNodeType == NodeType.GatewayMain ? NodeType.GatewaySide : NodeType.GatewayMain;

            string chain = primaryNodeType == NodeType.GatewayMain ? "main" : "side";

            string portSuffix = this.GetPortNumberSuffix(primaryNodeType, nodeIndex);
            string counterPortSuffix = this.GetPortNumberSuffix(counterNodeType, nodeIndex);

            nodeSetup
                .SetConsoleColor(GetConsoleColor(nodeIndex))
                .WithAgentPrefix($"fed{nodeIndex + 1}{chain}")
                .WithDataDir($"$root_datadir\\gateway{nodeIndex + 1}")
                .WithPort($"36{portSuffix}")
                .WithApiPort($"38{portSuffix}")
                .WithCounterChainApiPort($"38{counterPortSuffix}")
                .WithRedeemScript(this.scriptAndAddresses.payToMultiSig.ToString())
                .WithPublicKey(this.pubKeysByMnemonic[this.mnemonics[nodeIndex]].ToString());

            List<string> federationIps = chain == "main" ? this.mainFederationIps : this.sideFederationIps;

            //filter out current node from the federationIps
            nodeSetup.WithFederationIps(federationIps.Where(ip => !ip.Contains(nodeSetup.Port.ToString())));

            return nodeSetup;
        }

        private void SetupNodes()
        {
            // Federation Gateways
            for (int i = 0; i < this.federationMembersCount; i++)
            {
                this.configuredGatewayNodes.Add(NodeSetup
                    .Configure($"Gateway{i + 1} MAIN", NodeType.GatewayMain, NetworkType.Testnet, "$path_to_federationgatewayd", (nodeSetup) => this.DefaultGatewayConfigurator(nodeSetup, i))
                    .WithCustomArguments("-mincoinmaturity=1 -mindepositconfirmations=1")
                );

                this.configuredGatewayNodes.Add(NodeSetup
                    .Configure($"Gateway{i + 1} SIDE", NodeType.GatewaySide, NetworkType.Regtest, "$path_to_federationgatewayd", (nodeSetup) => this.DefaultGatewayConfigurator(nodeSetup, i))
                    .WithCustomArguments("-mincoinmaturity=1 -mindepositconfirmations=1 -txindex = 1")
                );
            }

            // MainChainUser.
            this.configuredUserNodes.Add(NodeSetup
                .Configure($"MAIN Chain User", NodeType.UserMain, NetworkType.Testnet, "$path_to_stratisd")
                .SetConsoleColor("0D")
                .WithAgentPrefix("mainuser")
                .WithDataDir($"$root_datadir\\MainchainUser")
                .WithPort($"36178")
                .WithApiPort($"38221")
                .AddNodes(new string[] {
                    "13.70.81.5",
                    "52.151.76.252"
                })
                .WithCustomArguments("-whitelist=52.151.76.252")
            );

            // SidechainUser.
            this.configuredUserNodes.Add(NodeSetup
                .Configure($"SIDE Chain User", NodeType.UserSide, NetworkType.Regtest, "$path_to_sidechaind")
                .SetConsoleColor("0C")
                .WithAgentPrefix("sideuser")
                .WithDataDir($"$root_datadir\\SidechainUser")
                .WithPort($"26179")
                .WithApiPort($"38225")
                .AddNodes(this.configuredGatewayNodes
                    .Where(node => node.NodeType == NodeType.GatewaySide)
                    .Select(node => $"127.0.0.1:{node.Port.ToString()}")
                )
            );
        }


        private void CreatePoaKeyFiles()
        {
            //for now just use the same private keys for multisig wallet and block signing
            for (int i = 0; i < this.federationMembersCount; i++)
            {
                var privateKey = this.mnemonics[i].DeriveExtKey().PrivateKey;
                var targetFile = $"$root_datadir\\gateway{i + 1}\\fedpeg\\FederatedPegRegTest\\federationKey.dat";

                var keyAsString = System.BitConverter.ToString(privateKey.ToBytes());
                this.AppendLine($"$mining_key_hex_{i + 1} = \"{keyAsString}\"");
                this.AppendLine($"$bytes_{i + 1} = foreach($hexByte in $mining_key_hex_{i + 1}.Split(\"-\")) {{[System.Convert]::ToByte($hexByte, 16)}}");
                this.AppendLine($"New-Item -path \"{targetFile}\" -type file");
                this.AppendLine($"$bytes_{i + 1} | set-content {targetFile} -Encoding Byte");
            };
            this.AppendLine(Environment.NewLine);
        }

        private void StartChainsD()
        {
            foreach (NodeSetup node in this.configuredUserNodes)
            {
                this.AppendLine($"# {node.NodeType}");
                CallStartNode(
                    path: node.DaemonPath,
                    title: node.Name,
                    color: node.ConsoleColor,
                    args: node.GenerateCommandArguments(),
                    timeout: "$interval_time"
               );
            }
        }

        private void StartGatewayDs()
        {
            this.AppendLine("#Federation members");
            foreach (NodeSetup node in this.configuredGatewayNodes)
            {
                CallStartNode(
                    path: node.DaemonPath,
                    title: node.Name,
                    color: node.ConsoleColor,
                    args: node.GenerateCommandArguments(),
                    timeout: "$long_interval_time"
               );

                if (node.NodeType == NodeType.GatewaySide)
                    this.AppendLine(Environment.NewLine);
            }
        }

        private void CopyStratisChainFiles()
        {
            // Create the folders in case they don't exist.
            this.AppendLine("# Create the folders in case they don't exist.");

            this.AppendLine("New-Item -ItemType directory -Force -Path $root_datadir");

            List<string> mainNetdestinationFolders = new List<string>();
            foreach (NodeSetup node in this.configuredGatewayNodes.Union(this.configuredUserNodes))
            {
                string dataDirFullPath = GetDataDirFullPath(node);
                this.AppendLine($@"New-Item -ItemType directory -Force -Path {dataDirFullPath}");

                if (node.NodeType == NodeType.GatewayMain || node.NodeType == NodeType.UserMain)
                    mainNetdestinationFolders.Add(dataDirFullPath);
            }


            this.AppendLine(Environment.NewLine);

            // Copy the blockchain data from a current, ideally up-to-date, Stratis Testnet folder.
            this.AppendLine("# Copy the blockchain data from a current, ideally up-to-date, Stratis Testnet folder.");
            this.AppendLine(@"If ((Test-Path $env:APPDATA\StratisNode\stratis\StratisTest) -And -Not (Test-Path $root_datadir\gateway1\stratis\StratisTest\blocks)) {");
            this.AppendLine($@"    $destinations = {string.Join(",\n\t\t", mainNetdestinationFolders.Select(f => $"\"{f}\""))}");
            this.AppendLine(@"    $destinations | % { Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\blocks -Recurse -Destination $_}");
            this.AppendLine(@"    $destinations | % { Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\chain -Recurse -Destination $_}");
            this.AppendLine(@"    $destinations | % { Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\coinview -Recurse -Destination $_}");
            this.AppendLine(@"    $destinations | % { Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\finalizedBlock -Recurse -Destination $_}");
            this.AppendLine(@"    $destinations | % { Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\provenheaders -Recurse -Destination $_}");

            string walletDestinationPath = this.configuredUserNodes.Where(node => node.NodeType == NodeType.UserMain).Select(node => GetDataDirFullPath(node)).FirstOrDefault();
            this.AppendLine($@"    Copy-Item -Path $path_to_stratis_wallet_with_funds -Destination {walletDestinationPath}");

            this.AppendLine(@"}");
            this.AppendLine(Environment.NewLine);
        }

        private void SetFolderVariables()
        {
            var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var rootDataDir = Path.Combine(appDataDir, "StratisNode", "federation");
            var fedGatewayDDir = Path.Combine("$git_repos_path", "FederatedSidechains", "src", "Stratis.FederationGatewayD");
            var sidechainDDir = Path.Combine("$git_repos_path", "FederatedSidechains", "src", "Stratis.SidechainD");
            var stratisDDir = Path.Combine("$git_repos_path", "StratisBitcoinFullNode", "src", "Stratis.StratisD");
            var walletFile = Path.Combine(appDataDir, "StratisNode", "stratis", this.mainchainNetwork.Name, "walletTest1.wallet.json");
            this.AppendLine("###############################");
            this.AppendLine("#    UPDATE THESE 8 VALUES    #");
            this.AppendLine("###############################");
            this.AppendLine($"$git_repos_path = \"{Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), "source", "repos")}\"");
            this.AppendLine($"$root_datadir = \"{rootDataDir}\"");
            this.AppendLine($"$path_to_federationgatewayd = \"{fedGatewayDDir}\"");
            this.AppendLine($"$path_to_sidechaind = \"{sidechainDDir}\"");
            this.AppendLine($"$path_to_stratisd = \"{stratisDDir}\"");
            this.AppendLine($"$path_to_stratis_wallet_with_funds = \"{walletFile}\"");
            this.AppendLine($"$dashboard_path = \"$root_datadir.\\dashboard.html\"");
            this.AppendLine($"$browser = \"chrome.exe\"");
            this.AppendLine(Environment.NewLine);
        }

        private void EnableWallets()
        {
            this.AppendLine("######### API Queries to enable federation wallets ###########");

            List<NodeType> chains = new List<NodeType> { NodeType.UserMain, NodeType.UserSide };

            chains.ForEach(c =>
            {
                this.AppendLine($"# {c}");
                for (int i = 0; i < this.federationMembersCount; i++)
                {
                    this.AppendLine($"$params = @{{ \"mnemonic\" = \"{this.mnemonics[i]}\"; \"password\" = \"password\" }}");
                    this.AppendLine(
                        $"Invoke-WebRequest -Uri http://localhost:38{GetPortNumberSuffix(c, i)}/api/FederationWallet/enable-federation -Method post -Body ($params|ConvertTo-Json) -ContentType \"application/json\"");
                    this.AppendLine("timeout $long_interval_time");
                    this.AppendLine(Environment.NewLine);
                };
            });
        }


        private void SetTimeoutVariables()
        {
            this.AppendLine("# The interval between starting the networks run, in seconds.");
            this.AppendLine("$interval_time = 5");
            this.AppendLine("$long_interval_time = 10");
            this.AppendLine(Environment.NewLine);
        }

        private void AddCommentedFederationDetails()
        {
            this.AppendLine("# FEDERATION DETAILS");
            for (int i = 0; i < this.federationMembersCount; i++)
            {
                this.AppendLine($"# Member{i + 1} mnemonic: {this.mnemonics[i]}");
                this.AppendLine($"# Member{i + 1} public key: {this.pubKeysByMnemonic[this.mnemonics[i]]}");
            };

            this.AppendLine($"# Redeem script: {this.scriptAndAddresses.payToMultiSig}");
            this.AppendLine($"# Sidechan P2SH: {this.scriptAndAddresses.sidechainMultisigAddress.ScriptPubKey}");
            this.AppendLine($"# Sidechain Multisig address: {this.scriptAndAddresses.sidechainMultisigAddress}");
            this.AppendLine($"# Mainchain P2SH: {this.scriptAndAddresses.mainchainMultisigAddress.ScriptPubKey}");
            this.AppendLine($"# Mainchain Multisig address: {this.scriptAndAddresses.mainchainMultisigAddress}");
            this.AppendLine(Environment.NewLine);
        }

        private void CallStartNode(string path, string title, string color, string args, string timeout)
        {
            this.AppendLine($@"Start-Node -Path {path} -WindowTitle ""{title}"" -ConsoleColor {color} -CmdArgs ""{args}"" -Timeout {timeout}");
        }

        private void CreateHelpers()
        {
            this.AppendResource($"{this.GetType().Namespace}.Resources.HelperMethods.ps1");
        }

        private void CreateDebuggingDashboard()
        {
            this.AppendLine(Environment.NewLine);
            this.AppendLine("#Creating Debugging Dashboard and opening it on $browser");
            this.AppendLine($"Create-Dashboard");
            this.AppendLine(Environment.NewLine);
        }
    }
}
