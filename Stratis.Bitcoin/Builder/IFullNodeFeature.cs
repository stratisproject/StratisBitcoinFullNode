namespace Stratis.Bitcoin {
   /// <summary>
   /// Defines methods for modules that are managed by the FullNode.
   /// </summary>
   public interface IFullNodeFeature {
      /// <summary>
      /// Triggered when the FullNode host has fully started
      /// </summary>
      void Start(FullNode fullNodeInstance);

      /// <summary>
      /// Triggered when the FullNode is performing a graceful shutdown.
      /// Requests may still be in flight. Shutdown will block until this event completes.
      /// </summary>
      void Stop();
   }
}