using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Stratis.SmartContracts.Core.Deployment;

namespace Stratis.SmartContracts.Tools.Sct.Deployment
{
    [Command(Description = "Deploys a smart contract to the given node")]
    [HelpOption]
    class Deployer
    {
        private static readonly HttpClient client = new HttpClient();

        private static readonly string DeploymentResource = "/api/SmartContracts/build-and-send-create";

        public Deployer()
        {
            this.AccountName = "account 0";
            this.GasPrice = "1";
            this.GasLimit = "10000";
        }

        [Argument(0, Description = "The source code of the smart contract to deploy", Name = "<File>")]
        [Required]
        public string InputFile { get; }

        [Argument(1, Description = "The initial node to deploy the smart contract to", Name = "<Node>")]
        [Required]
        [Url]
        public string Node { get; }

        [Option("-wallet|--wallet", CommandOptionType.SingleValue, Description = "Wallet name")]
        [Required]
        public string WalletName { get; }

        [Option("-account|--account", CommandOptionType.SingleValue, Description = "Account name")]
        public string AccountName { get; }

        [Option("-fee|--feeamount", CommandOptionType.SingleValue, Description = "Fee amount")]
        [Required]
        public string FeeAmount { get; }

        [Option("-password|--password", CommandOptionType.SingleValue, Description = "Password")]
        [Required]
        public string Password { get; }

        [Option("-amount|--amount", CommandOptionType.SingleValue, Description = "Amount")]
        public string Amount { get; }

        [Option("-gasprice|--gasprice", CommandOptionType.SingleValue, Description = "Gas price")]
        public string GasPrice { get; }

        [Option("-gaslimit|--gaslimit", CommandOptionType.SingleValue, Description = "Gas limit")]
        public string GasLimit { get; }

        [Option("-sender|--sender", CommandOptionType.SingleValue, Description = "Sender address")]
        [Required]
        public string Sender { get; }

        [Option("-params|--params", CommandOptionType.MultipleValue, Description = "Params to be passed to the constructor when instantiating the contract")]
        public string[] Params { get; }

        private async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            console.WriteLine();
            console.WriteLine("Smart Contract Deployer");
            console.WriteLine();

            if (!File.Exists(this.InputFile))
            {
                console.WriteLine($"{this.InputFile} does not exist");
                return 1;
            }

            string source;

            console.WriteLine($"Reading {this.InputFile}");

            using (var sr = new StreamReader(File.OpenRead(this.InputFile)))
            {
                source = sr.ReadToEnd();
            }

            console.WriteLine($"Read {this.InputFile} OK");
            console.WriteLine();

            if (string.IsNullOrWhiteSpace(source))
            {
                console.WriteLine($"Empty file at {this.InputFile}");
                return 1;
            }

            ValidationServiceResult validationResult = new ValidatorService().Validate(this.InputFile, source, console, this.Params);
            if (!validationResult.Success)
                return 1;
            else
                console.WriteLine("Validation passed!");

            await DeployAsync(validationResult.CompilationResult.Compilation, console);
            console.WriteLine();

            return 1;
        }

        private async Task DeployAsync(byte[] compilation, IConsole console)
        {
            console.WriteLine("Deploying to node ", this.Node);

            var model = new BuildCreateContractTransactionRequest();
            model.ContractCode = compilation.ToHexString();
            model.AccountName = this.AccountName;
            model.FeeAmount = this.FeeAmount;
            model.Amount = this.Amount;
            model.GasPrice = this.GasPrice;
            model.GasLimit = this.GasLimit;
            model.Password = this.Password;
            model.WalletName = this.WalletName;
            model.Sender = this.Sender;
            model.Parameters = this.Params;

            var deployer = new HttpContractDeployer(client, DeploymentResource);

            DeploymentResult response = await deployer.DeployAsync(this.Node, model);

            console.WriteLine(string.Empty);

            if (response.Success)
            {
                console.WriteLine("Contract creation transaction successful!");
                console.WriteLine($"Transaction Id: {response.TransactionId}");
                console.WriteLine($"Contract Address: {response.ContractAddress}");
                return;
            }

            console.WriteLine(string.Format("Deployment Error: {0}", response.Message));
            foreach (string error in response.Errors)
            {
                console.WriteLine(error);
            }
        }
    }
}