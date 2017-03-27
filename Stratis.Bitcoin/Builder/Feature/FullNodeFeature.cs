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
	/// It can can manage its life time or use the full node disposable resources.

	/// If a feature is added functionality available to use by the node
	/// (but may be disabled/enabled by the configuration) the naming convention is
	/// Add[Feature](). Conversely, when a features is inclined to be used if included,
	/// the naming convention should be Use[Feature]()

	/// </summary>
	public abstract class FullNodeFeature : IFullNodeFeature
	{
		public abstract void Start();

		public virtual void Stop()
		{
		}
	}
}