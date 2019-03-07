using System.Collections.Generic;

namespace Stratis.DB
{
    public interface IStratisDBTrackers
    {
        /// <summary>
        /// Creates trackers for recording changes.
        /// </summary>
        /// <returns>Trackers for recording changes.</returns>
        Dictionary<string, IStratisDBTracker> CreateTrackers(string[] tables);

        /// <summary>
        /// Called when changes to the database are committed.
        /// </summary>
        /// <param name="trackers">The trackers which were created in <see cref="CreateTrackers"/>.</param>
        /// <remarks>This method is intended be called by the <see cref="StratisDBTransaction"/> class.</remarks>
        void OnCommit(Dictionary<string, IStratisDBTracker> trackers);
    }
}
