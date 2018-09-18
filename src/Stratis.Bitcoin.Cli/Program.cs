using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NBitcoin;
using NBitcoin.Networks;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Cli
{
    public class Program
    {
        /// <summary>
        /// The expected sequence of arguments:
        /// <list>
        /// <item>
        /// 1, [network-name] [options] [rpc-command] [rpc-params].
        /// </item>
        /// <item>
        /// 2, [network-name] [options] [api-controller "/" api-command] [api-params].
        /// </item>
        /// </list>
        /// </summary>
        public static void Main(string[] args)
        {
            try
            {
                // Preprocess the command line arguments
                var argList = new List<string>(args);

                string networkName = null;
                if (argList.Any())
                {
                    networkName = argList.First();
                    argList.RemoveAt(0);
                }

                var optionList = new List<string>();
                while ((argList.Any()) && (argList[0].StartsWith('-')))
                {
                    optionList.Add(argList[0]);
                    argList.RemoveAt(0);
                }

                string command = string.Empty;
                if (argList.Any())
                {
                    command = argList.First();
                    argList.RemoveAt(0);
                }

                var commandArgList = new List<string>(argList);

                // Display help if required.
                if (optionList.Contains("-help") || optionList.Contains("--help") || string.IsNullOrWhiteSpace(command))
                {
                    var builder = new StringBuilder();
                    builder.AppendLine("Usage:");
                    builder.AppendLine(" dotnet run <Stratis.Bitcoin.Cli/Stratis.Bitcoin.Cli.dll> [network-name] [options] <command> [arguments]");
                    builder.AppendLine();
                    builder.AppendLine("Command line arguments:");
                    builder.AppendLine();
                    builder.AppendLine("[network-name]                     Name of the network - e.g. \"stratis\", \"stratismain\", \"stratistest\", \"bitcoinmain\", \"bitcointest\".");
                    builder.AppendLine("[options]                          Options for the CLI (optional) - e.g. -help, -rpcuser, see below.");
                    builder.AppendLine("[command]                          Name of RPC method or API <controller>/<method>.");
                    builder.AppendLine("[arguments]                        Argument by position (RPC) or Name = Value pairs (API) (optional).");
                    builder.AppendLine();
                    builder.AppendLine("Options:");
                    builder.AppendLine("-help                              This help message");
                    builder.AppendLine("-rpcconnect=<ip>                   Send commands to node running on <ip> (default: 127.0.0.1)");
                    builder.AppendLine("-rpcport=<port>                    Connect to JSON-RPC on <port> (default for Stratis: 26174 or default for Bitcoin: 8332)");
                    builder.AppendLine("-rpcuser=<user>                    Username for JSON-RPC connections");
                    builder.AppendLine("-rpcpassword=<pw>                  Password for JSON-RPC connections");
                    builder.AppendLine();
                    builder.AppendLine("Examples:");
                    builder.AppendLine();
                    builder.AppendLine("dotnet run stratis Wallet/history WalletName=testwallet - Lists all the historical transactions of the wallet called 'testwallet'.");
                    builder.AppendLine("dotnet run stratis getinfo -rpcuser=stratistestuser -rpcpassword=stratistestpassword -rpcconnect=127.0.0.3 -rpcport=26174 - Displays general information about the Stratis node on the 127.0.0.3:26174, authenticating with the RPC specified user.");
                    builder.AppendLine("dotnet run bitcoin getbalance -rpcuser=btctestuser -rpcpassword=btctestpass - Displays the current balance of the opened wallet on the 127.0.0.1:8332 node, authenticating with the RPC specified user.");
                    Console.WriteLine(builder);
                    return;
                }

                // Determine API port.
                int defaultRestApiPort = 0;
                Network network = null;

                if (networkName.Contains("stratis"))
                {
                    defaultRestApiPort = 37221;
                    network = NetworkRegistration.Register(new StratisMain());
                }
                else
                {
                    defaultRestApiPort = 37220;
                    network = NetworkRegistration.Register(new BitcoinMain());
                }

                // API calls require both the contoller name and the method name separated by "/".
                // If this is not an API call then assume it is an RPC call.
                if (!command.Contains("/"))
                {
                    // Process RPC call.
                    try
                    {
                        string[] options = optionList.Append("-server").ToArray();

                        var nodeSettings = new NodeSettings(network, args:options);

                        var rpcSettings = new RpcSettings(nodeSettings);

                        // Find the binding to 127.0.0.1 or the first available. The logic in RPC settings ensures there will be at least 1.
                        System.Net.IPEndPoint nodeEndPoint = rpcSettings.Bind.FirstOrDefault(b => b.Address.ToString() == "127.0.0.1") ?? rpcSettings.Bind[0];
                        var rpcUri = new Uri($"http://{nodeEndPoint}");

                        // Process the command line RPC arguments
                        // TODO: this should probably be moved to the NodeSettings.FromArguments
                        if (options.GetValueOf("-rpcbind") != null)
                        {
                            rpcUri = new Uri($"http://{options.GetValueOf("-rpcbind")}");
                        }

                        if (options.GetValueOf("-rpcconnect") != null || options.GetValueOf("-rpcport") != null)
                        {
                            string rpcAddress = options.GetValueOf("-rpcconnect") ?? "127.0.0.1";

                            int rpcPort = rpcSettings.RPCPort;
                            int.TryParse(options.GetValueOf("-rpcport"), out rpcPort);

                            rpcUri = new Uri($"http://{rpcAddress}:{rpcPort}");
                        }
                        rpcSettings.RpcUser = options.GetValueOf("-rpcuser") ?? rpcSettings.RpcUser;
                        rpcSettings.RpcPassword = options.GetValueOf("-rpcpassword") ?? rpcSettings.RpcPassword;

                        Console.WriteLine($"Connecting to the following RPC node: http://{rpcSettings.RpcUser}:{rpcSettings.RpcPassword}@{rpcUri.Authority}.");

                        // Initialize the RPC client with the configured or passed userid, password and endpoint.
                        var rpcClient = new RPCClient($"{rpcSettings.RpcUser}:{rpcSettings.RpcPassword}", rpcUri, network);

                        // Execute the RPC command
                        Console.WriteLine($"Sending RPC command '{command} {string.Join(" ", commandArgList)}' to '{rpcUri}'.");
                        RPCResponse response = rpcClient.SendCommand(command, commandArgList.ToArray());

                        // Return the result as a string to the console.
                        Console.WriteLine(response.ResultString);
                    }
                    catch (Exception err)
                    {
                        Console.WriteLine(err.Message);
                    }
                }
                else
                {
                    // Process API call.
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Accept.Clear();
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        string url = $"http://localhost:{defaultRestApiPort}/api/{command}";
                        if (commandArgList.Any()) url += $"?{string.Join("&", commandArgList)}";
                        try
                        {
                            // Get the response.
                            Console.WriteLine($"Sending API command to {url}.");
                            string response = client.GetStringAsync(url).GetAwaiter().GetResult();

                            // Format and return the result as a string to the console.
                            Console.WriteLine(JsonConvert.SerializeObject(JsonConvert.DeserializeObject<object>(response), Formatting.Indented));
                        }
                        catch (Exception err)
                        {
                            Console.WriteLine(ExceptionToString(err));
                        }
                    }
                }
            }
            catch (Exception err)
            {
                // Report any errors to the console.
                Console.WriteLine(ExceptionToString(err));
            }
        }

        /// <summary>
        /// Determine both the exception message and any inner exception messages.
        /// </summary>
        /// <param name="exception">The exception object.</param>
        /// <returns>Returns the exception message plus any inner exceptions.</returns>
        public static string ExceptionToString(Exception exception)
        {
            bool isDebugMode = false;
#if DEBUG
            isDebugMode = true;
#endif
            Exception ex = exception;
            var stringBuilder = new StringBuilder(128);
            while (ex != null)
            {
                if (isDebugMode)
                    stringBuilder.Append($"{ex.GetType().Name}: ");
                stringBuilder.Append(ex.Message);
                if (isDebugMode)
                    stringBuilder.AppendLine(ex.StackTrace);
                ex = ex.InnerException;
                if (ex != null)
                    stringBuilder.Append(" ---> ");
            }
            return stringBuilder.ToString();
        }
    }
}