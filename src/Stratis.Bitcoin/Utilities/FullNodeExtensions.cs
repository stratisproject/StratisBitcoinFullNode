using System;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;

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
                        Console.WriteLine("Application is shutting down.");
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
                    await node.RunAsync(cts.Token, "Application started. Press Ctrl+C to shut down.", "Application stopped.").ConfigureAwait(false);
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
        /// <param name="shutdownMessage">Message to display on the console to instruct the user on how to invoke the shutdown.</param>
        /// <param name="shutdownCompleteMessage">Message to display on the console when the shutdown is complete.</param>
        public static async Task RunAsync(this IFullNode node, CancellationToken cancellationToken, string shutdownMessage, string shutdownCompleteMessage)
        {
            node.Start();

            if (!string.IsNullOrEmpty(shutdownMessage))
            {
                Console.WriteLine();
                Console.WriteLine(shutdownMessage);
                Console.WriteLine();
            }

            cancellationToken.Register(state =>
            {
                ((INodeLifetime)state).StopApplication();
            },
            node.NodeLifetime);

            var waitForStop = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            node.NodeLifetime.ApplicationStopping.Register(obj =>
            {
                var tcs = (TaskCompletionSource<object>)obj;
                tcs.TrySetResult(null);
            }, waitForStop);

            await waitForStop.Task.ConfigureAwait(false);

            node.Dispose();

            if (!string.IsNullOrEmpty(shutdownCompleteMessage))
            {
                Console.WriteLine();
                Console.WriteLine(shutdownCompleteMessage);
                Console.WriteLine();
            }
        }
    }
}
