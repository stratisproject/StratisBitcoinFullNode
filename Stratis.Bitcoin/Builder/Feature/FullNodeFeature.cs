namespace Stratis.Bitcoin.Builder.Feature
{
	/// <summary>
	/// Defines methods for features that are managed by the FullNode.
	/// </summary>
	public interface IFullNodeFeature
	{
		/// <summary>
		/// Triggered when the FullNode host has fully started
		/// </summary>
		void Start();

		/// <summary>
		/// Triggered when the FullNode is performing a graceful shutdown.
		/// Requests may still be in flight. Shutdown will block until this event completes.
		/// </summary>
		void Stop();
	}

	/// <summary>
	/// A feature is used to extend functionality into the full node
	/// It can can manage its life time or use the full node disposable resources
	/// </summary>
	public abstract class FullNodeFeature : IFullNodeFeature
	{
		public abstract void Start();

		public virtual void Stop()
		{
		}
	}
}