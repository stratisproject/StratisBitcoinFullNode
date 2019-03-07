namespace Stratis.DB
{
    /// <summary>
    /// Enumerates the database events to keep track of.
    /// </summary>
    public enum StratisDBEvent
    {
        ObjectCreated,
        ObjectRead,
        ObjectWritten,
        ObjectDeleted
    }

    /// <summary>
    /// Tracks changes made to objects.
    /// </summary>
    public interface IStratisDBTracker
    {
        /// <summary>
        /// Records the object event.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="ev">The event.</param>
        void ObjectEvent(object obj, StratisDBEvent ev);
    }
}
