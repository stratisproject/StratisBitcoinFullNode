using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Builder
{
	public interface IFullNodeFeatureExecutor
	{
		void Start();
		void Stop();
	}

	/// <summary>
	/// Borrowed from asp.net
	/// </summary>
	public class FullNodeFeatureExecutor : IFullNodeFeatureExecutor
	{
		private readonly IFullNode node;
	    private readonly ILogger logger;

		public FullNodeFeatureExecutor(IFullNode fullNode, ILoggerFactory loggerFactory)
		{
			Guard.NotNull(fullNode, nameof(fullNode));

			this.node = fullNode;
		    this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
		}

		public void Start()
		{
			try
			{
				this.Execute(service => service.Start());
			}
			catch (Exception ex)
			{
			    this.logger.LogError("An error occurred starting the application", ex);
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
			    this.logger.LogError("An error occurred stopping the application", ex);
				throw;
			}
		}

		private void Execute(Action<IFullNodeFeature> callback)
		{
			List<Exception> exceptions = null;

			if (this.node.Services != null)
			{
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
}