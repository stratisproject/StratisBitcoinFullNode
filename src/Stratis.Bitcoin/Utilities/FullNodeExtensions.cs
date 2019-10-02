using System;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Extension methods for IFullNode interface.
    /// </summary>
    public static class FullNodeExtensions
    {
        /// <summary>
        /// Installs handlers for graceful shutdown in the console, starts a full node and waits until it terminates.
        /// </summary>
        /// <param name="node">Full node to run.</param>
        public static async Task RunAsync(this IFullNode node)
        {
            var done = new ManualResetEventSlim(false);
            using (var cts = new CancellationTokenSource())
            {
                Action shutdown = () =>
                {
                    if (!cts.IsCancellationRequested)
                    {
                        Console.WriteLine("Application is shutting down...");
                        try
                        {
                            cts.Cancel();
                        }
                        catch (ObjectDisposedException exception)
                        {
                            Console.WriteLine(exception.Message);
                        }
                    }

                    done.Wait();
                };

                AssemblyLoadContext assemblyLoadContext = AssemblyLoadContext.GetLoadContext(typeof(FullNode).GetTypeInfo().Assembly);
                assemblyLoadContext.Unloading += context => shutdown();

                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    shutdown();
                    // Don't terminate the process immediately, wait for the Main thread to exit gracefully.
                    eventArgs.Cancel = true;
                };

                try
                {
                    await node.RunAsync(cts.Token).ConfigureAwait(false);
                }
                finally
                {
                    done.Set();
                }
            }
        }

        /// <summary>
        /// Starts a full node, sets up cancellation tokens for its shutdown, and waits until it terminates.
        /// </summary>
        /// <param name="node">Full node to run.</param>
        /// <param name="cancellationToken">Cancellation token that triggers when the node should be shut down.</param>
        public static async Task RunAsync(this IFullNode node, CancellationToken cancellationToken)
        {
            // node.NodeLifetime is not initialized yet. Use this temporary variable as to avoid side-effects to node.
            var nodeLifetime = node.Services.ServiceProvider.GetRequiredService<INodeLifetime>() as NodeLifetime;

            cancellationToken.Register(state =>
            {
                ((INodeLifetime)state).StopApplication();
            },
            nodeLifetime);

            var waitForStop = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            nodeLifetime.ApplicationStopping.Register(obj =>
            {
                var tcs = (TaskCompletionSource<object>)obj;
                tcs.TrySetResult(null);
            }, waitForStop);

            Console.WriteLine();
            Console.WriteLine("Application starting, press Ctrl+C to cancel.");
            Console.WriteLine();

            node.Start();

            Console.WriteLine();
            Console.WriteLine("Application started, press Ctrl+C to stop.");
            Console.WriteLine();

            await waitForStop.Task.ConfigureAwait(false);

            node.Dispose();

            Console.WriteLine();
            Console.WriteLine("Application stopped.");
            Console.WriteLine();
        }
    }
}
