using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.SmartContracts.Core.ContractValidation;

namespace Stratis.SmartContracts.Tools.Sct.Deployment
{
    [Command(Description = "Deploys a smart contract to the given node")]
    [HelpOption]
    class Deployer
    {
        private static readonly HttpClient client = new HttpClient();

        private static readonly string DeploymentResource = "/api/SmartContracts/build-and-send-create";

        [Argument(0, Description = "The source code of the smart contract to deploy",
            Name = "<File>")]
        [Required]
        public string InputFile { get; }

        [Argument(1, Description = "The initial node to deploy the smart contract to",
            Name = "<Node>")]
        [Required]
        [Url]
        public string Node { get; }

        [Option("-wallet|--wallet", CommandOptionType.SingleValue, Description = "Wallet name")]
        [Required]
        public string WalletName { get; }

        [Option("-account|--account", CommandOptionType.SingleValue, Description = "Account name")]
        [Required]
        public string AccountName { get; }

        [Option("-amount|--amount", CommandOptionType.SingleValue, Description = "Amount")]
        [Required]
        public string Amount { get; }

        [Option("-fee|--feeamount", CommandOptionType.SingleValue, Description = "Fee amount")]
        [Required]
        public string FeeAmount { get; }

        [Option("-p|--password", CommandOptionType.SingleValue, Description = "Password")]
        [Required]
        public string Password { get; }

        [Option("-gasprice|--gasprice", CommandOptionType.SingleValue, Description = "Gas price")]
        [Required]
        public string GasPrice { get; }

        [Option("-gaslimit|--gaslimit", CommandOptionType.SingleValue, Description = "Gas limit")]
        [Required]
        public string GasLimit { get; }

        [Option("-sender|--sender", CommandOptionType.SingleValue, Description = "Sender address")]
        [Required]
        public string Sender { get; }

        [Option("-params|--params", CommandOptionType.MultipleValue, Description = "Params to be passed to the constructor when instantiating the contract")]
        public string[] Params { get; }

        private async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("Smart Contract Deployer");

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

            if (string.IsNullOrWhiteSpace(source))
            {
                console.WriteLine($"Empty file at {this.InputFile}");
                return 1;
            }

            if (!ValidateFile(this.InputFile, source, console, out SmartContractCompilationResult compilationResult))
            {
                console.WriteLine("Smart Contract failed validation. Run validate [FILE] for more info.");
                return 1;
            }

            await this.DeployAsync(compilationResult.Compilation, console);

            return 1;
        }

        private async Task DeployAsync(byte[] compilation, IConsole console)
        {
            console.WriteLine("Deploying to node ", this.Node);

            var model = new BuildCreateContractTransactionRequest();
            model.ContractCode = compilation.ToHexString();
            model.AccountName = this.AccountName;
            model.Amount = this.Amount;
            model.FeeAmount = this.FeeAmount;
            model.GasPrice = this.GasPrice;
            model.GasLimit = this.GasLimit;
            model.Password = this.Password;
            model.WalletName = this.WalletName;
            model.Sender = this.Sender;
            model.Parameters = Params;

            var json = NetJSON.NetJSON.Serialize(model);

            HttpResponseMessage response = await client.PostAsync(this.GetDeploymentUri(), new StringContent(json, Encoding.UTF8, "application/json"));

            console.WriteLine("");

            if (response.IsSuccessStatusCode)
            {
                console.WriteLine("Deployment Success");

                string successJson = await response.Content.ReadAsStringAsync();
                BuildCreateContractTransactionResponse successResponse =
                    NetJSON.NetJSON.Deserialize<BuildCreateContractTransactionResponse>(successJson);

                console.WriteLine($"Contract successfully deployed");
                console.WriteLine($"Address: {successResponse.NewContractAddress}");

                return;
            }

            string errorJson = await response.Content.ReadAsStringAsync();
            ErrorResponse resp = NetJSON.NetJSON.Deserialize<ErrorResponse>(errorJson);
            console.WriteLine("Deployment Error!");

            foreach (ErrorModel err in resp.Errors)
            {
                console.WriteLine(err.Message);
            }            
        }

        private Uri GetDeploymentUri()
        {
            return new Uri(new Uri(this.Node), DeploymentResource);
        }

        public static bool ValidateFile(string fileName, string source, IConsole console, out SmartContractCompilationResult compilationResult)
        {
            var determinismValidator = new SmartContractDeterminismValidator();
            var formatValidator = new SmartContractFormatValidator();

            console.WriteLine($"Compiling...");
            compilationResult = SmartContractCompiler.Compile(source);

            if (!compilationResult.Success)
            {
                console.WriteLine("Compilation failed!");
            }

            console.WriteLine($"Compilation OK");

            byte[] compilation = compilationResult.Compilation;

            console.WriteLine("Building ModuleDefinition");

            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(compilation, new DotNetCoreAssemblyResolver());

            console.WriteLine("ModuleDefinition built successfully");

            console.WriteLine($"Validating file {fileName}...");

            SmartContractValidationResult formatValidationResult = formatValidator.Validate(decompilation);

            SmartContractValidationResult determinismValidationResult = determinismValidator.Validate(decompilation);

            return compilationResult.Success 
                   && formatValidationResult.IsValid 
                   && determinismValidationResult.IsValid;
        }
    }
}