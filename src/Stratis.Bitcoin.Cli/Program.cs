using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.RPC;

namespace Stratis.Bitcoin.Cli
{
    class Program
    {
        /// <summary>
        /// The expected sequence of arguments:
        ///    1) [network-name] [rpc-command] [rpc-params].
        /// OR
        ///    2) [network-name] [api-controller "/" api-command] [api-params].
        /// </summary>
        public static void Main(string[] args)
        {
            try
            {
                // Display help if required.
                if (args.Length == 0 || args.Contains("-help") || args.Contains("--help"))
                {
                    var builder = new StringBuilder();
                    builder.AppendLine("Usage:");
                    builder.AppendLine(" dotnet run <Stratis.Bitcoin.Cli/Stratis.Bitcoin.Cli.dll> [network-name] [command] [arguments]");
                    builder.AppendLine();
                    builder.AppendLine("Command line arguments:");
                    builder.AppendLine();
                    builder.AppendLine("[network-name]            Name of the network - e.g. \"stratis\".");
                    builder.AppendLine("[command]                 Name of RPC method or API <controller>/<method>.");
                    builder.AppendLine("[arguments]               Argument by position (RPC) or Name = Value pairs (API).");
                    Console.WriteLine(builder);
                    return;
                }
            
                // Determine API port.
                string blockchain = "bitcoin";
                int apiPort = 37220;
                if (args.Any(a => a.Contains("stratis")))
                {
                    blockchain = "stratis";
                    apiPort = 37221;
                    // hack until static flags are removed.
                    var s = Network.StratisMain;
                    var st = Network.StratisTest;
                }

                // The first argument is the network name.
                var network = Network.GetNetwork(args.First());

                // API calls require both the contoller name and the method name separated by "/".
                // If this is not an API call then assume it is an RPC call.
                if (!args.ElementAt(1).Contains("/"))
                {
                    // Process RPC call.
                    try
                    {
                        NodeSettings nodeSettings = NodeSettings.FromArguments(args, blockchain, network);
                        var rpcSettings = new RpcSettings();
                        rpcSettings.Load(nodeSettings);

                        // Find the binding to 127.0.0.1 or the first available. The logic in RPC settings ensures there will be at least 1.
                        System.Net.IPEndPoint nodeEndPoint = rpcSettings.Bind.FirstOrDefault(b => b.Address.ToString() == "127.0.0.1") ?? rpcSettings.Bind[0];

                        // Initilize the RPC client with the configured or passed userid, password and endpoint.
                        RPCClient rpc = new RPCClient($"{rpcSettings.RpcUser}:{rpcSettings.RpcPassword}", new Uri($"http://{nodeEndPoint}"));

                        // Execute the RPC command
                        Console.WriteLine($"Sending RPC command '{string.Join(" ", args.Skip(1))}' to 'http://{nodeEndPoint}'...");
                        RPCResponse response = rpc.SendCommand(args.ElementAt(1), args.Skip(2).ToArray());

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
                        var url = $"http://localhost:{apiPort}/api/{args.ElementAt(1)}?{string.Join("&", args.Skip(2))}";
                        try
                        {
                            // Get the response.
                            Console.WriteLine($"Sending API command to {url}...");
                            var response = client.GetStringAsync(url).GetAwaiter().GetResult();
                            
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
            StringBuilder stringBuilder = new StringBuilder(128);
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
