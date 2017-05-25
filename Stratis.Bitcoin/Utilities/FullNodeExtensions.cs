using System;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Logging;

namespace Stratis.Bitcoin.Utilities
{
	public static class FullNodeExtensions
	{
		public static void Run(this IFullNode node)
		{
			var done = new ManualResetEventSlim(false);
			using (CancellationTokenSource cts = node.GlobalCancellation.Cancellation)
			{
				Action shutdown = () =>
				{
					if (!cts.IsCancellationRequested)
					{
						Logs.FullNode.LogInformation("Application is shutting down...");
						try
						{
							cts.Cancel();
						}
						catch (ObjectDisposedException exception)
						{
							Logs.FullNode.LogError(exception.Message);
						}
					}

					done.Wait();
				};

				var assemblyLoadContext = AssemblyLoadContext.GetLoadContext(typeof(FullNode).GetTypeInfo().Assembly);
				assemblyLoadContext.Unloading += context => shutdown();
				Console.CancelKeyPress += (sender, eventArgs) =>
				{
					shutdown();
					// Don't terminate the process immediately, wait for the Main thread to exit gracefully.
					eventArgs.Cancel = true;
				};

				node.Run(cts.Token, "Application started. Press Ctrl+C to shut down.");
				done.Set();
			}
		}

		public static void Run(this IFullNode node, CancellationToken cancellationToken, string shutdownMessage)
		{
			using (node)
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
					((IApplicationLifetime)state).StopApplication();
				},
				node.ApplicationLifetime);

				var waitForStop = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
				node.ApplicationLifetime.ApplicationStopping.Register(obj =>
				{
					var tcs = (TaskCompletionSource<object>) obj;
					tcs.TrySetResult(null);
				}, waitForStop);

				//await waitForStop.Task;
				waitForStop.Task.GetAwaiter().GetResult();

				node.Stop();
			}
		}
	}
}