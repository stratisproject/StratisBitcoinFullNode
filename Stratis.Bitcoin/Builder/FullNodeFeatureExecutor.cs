using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder.Feature;

namespace Stratis.Bitcoin.Builder
{
	/// <summary>
	/// Borrowed from asp.net
	/// </summary>
	public class FullNodeFeatureExecutor
	{
		private readonly FullNode node;

		public FullNodeFeatureExecutor(FullNode node)
		{
			this.node = node;
		}

		public void Start()
		{
			try
			{
				Execute(service => service.Start());
			}
			catch (Exception ex)
			{
				Logging.Logs.FullNode.LogError("An error occurred starting the application", ex);
				throw;
			}
		}

		public void Stop()
		{
			try
			{
				Execute(service => service.Stop());
			}
			catch (Exception ex)
			{
				Logging.Logs.FullNode.LogError("An error occurred stopping the application", ex);
				throw;
			}
		}

		private void Execute(Action<IFullNodeFeature> callback)
		{
			List<Exception> exceptions = null;

			foreach (var service in this.node.Services.Features)
			{
				try
				{
					callback(service);
				}
				catch (Exception ex)
				{
					if (exceptions == null)
					{
						exceptions = new List<Exception>();
					}

					exceptions.Add(ex);
				}
			}

			// Throw an aggregate exception if there were any exceptions
			if (exceptions != null)
			{
				throw new AggregateException(exceptions);
			}
		}
	}
}