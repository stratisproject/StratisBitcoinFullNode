namespace Stratis.Bitcoin {
   /// <summary>
   /// Defines methods for services that are managed by the FullNode.
   /// </summary>
   public interface IFullNodeService {
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
}