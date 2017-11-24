using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base
{
    /// <summary>
    /// Contract of a store of block header hashes that are considered invalid.
    /// </summary>
    public interface IInvalidBlockHashStore
    {
        /// <summary>
        /// Check if a block is marked as invalid.
        /// </summary>
        /// <param name="hashBlock">The block hash to check.</param>
        /// <returns><c>true</c> if the block is marked as invalid, <c>false</c> otherwise.</returns>
        bool IsInvalid(uint256 hashBlock);

        /// <summary>
        /// Marks a block as invalid.
        /// </summary>
        /// <param name="hashBlock">The block hash to mark as invalid.</param>
        /// <param name="rejectedUntil">Time in UTC after which the block is no longer considered as invalid, or <c>null</c> if the block is to be considered invalid forever.</param>
        void MarkInvalid(uint256 hashBlock, DateTime? rejectedUntil = null);
    }

    /// <summary>
    /// In memory store of invalid block header hashes.
    /// </summary>
    /// <remarks>
    /// The store has a limited capacity. When a new block header hash is marked as invalid
    /// once the capacity is reached, the oldest entry is removed and no longer considered invalid.
    /// <para>
    /// Entries with specified expiration time are either removed just like other entries - i.e. during
    /// an add operation when a capacity is reached, or they are removed when they are touched
    /// and it is detected that their expiration time is no longer in the future.
    /// </para>
    /// </remarks>
    public class InvalidBlockHashStore : IInvalidBlockHashStore
    {
        /// <summary>Default value for the maximal number of hashes we can store.</summary>
        public const int DefaultCapacity = 1000;

        /// <summary>A provider of the date and time.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Lock object to protect access to <see cref="invalidBlockHashesExpirations"/> and <see cref="orderedHashList"/>.</summary>
        private readonly object lockObject = new object();

        /// <summary>
        /// Collection of block header hashes that are to be considered invalid. If the value of the entry is not <c>null</c>,
        /// the entry is considered invalid only for a certain amount of time.
        /// </summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly Dictionary<uint256, DateTime?> invalidBlockHashesExpirations;

        /// <summary>Circular array of block header hash entries to allow quick removal of the oldest entry once the capacity is reached.</summary>
        /// <remarks>
        /// All access to this object has to be protected by <see cref="lockObject"/>.
        /// <para>The field is internal for testing purposes.</para>
        /// </remarks>
        internal readonly CircularArray<uint256> orderedHashList;

        /// <summary>
        /// Initializes the instance of the object.
        /// </summary>
        /// <param name="capacity">Maximal number of hashes we can store.</param>
        public InvalidBlockHashStore(IDateTimeProvider dateTimeProvider, int capacity = DefaultCapacity)
        {
            this.dateTimeProvider = dateTimeProvider;

            this.invalidBlockHashesExpirations = new Dictionary<uint256, DateTime?>(capacity);
            this.orderedHashList = new CircularArray<uint256>(capacity);
        }

        /// <inheritdoc />
        public bool IsInvalid(uint256 hashBlock)
        {
            bool res = false;

            lock (this.lockObject)
            {
                // First check if the entry exists.
                DateTime? expirationTime;
                if (this.invalidBlockHashesExpirations.TryGetValue(hashBlock, out expirationTime))
                {
                    // The block is banned forever if the expiration date was not set,
                    // or it is banned temporarily if it was set.
                    if (expirationTime != null)
                    {
                        // The block is still invalid now if the expiration date is still in the future.
                        res = expirationTime > this.dateTimeProvider.GetUtcNow();

                        // If the expiration date is not in the future anymore, remove the record from the list.
                        // Note that this will leave entry in the orderedHashList, but that is OK,
                        // that entry will be removed later.
                        if (!res) this.invalidBlockHashesExpirations.Remove(hashBlock);
                    }
                    else res = true;
                }
            }

            return res;
        }

        /// <inheritdoc />
        public void MarkInvalid(uint256 hashBlock, DateTime? rejectedUntil = null)
        {
            lock (this.lockObject)
            {
                DateTime? expirationTime;
                bool existsAlready = this.invalidBlockHashesExpirations.TryGetValue(hashBlock, out expirationTime);
                if (existsAlready)
                {
                    // Entry is existing already, only replace its value if the ban is stronger.
                    // This happens if the new ban is forever - i.e. rejectedUntil is null,
                    // or if the existing ban is not forever and the rejectedUntil value is greater
                    // than existing expiration time.
                    bool strongerBan = (rejectedUntil == null) || ((expirationTime != null) && (rejectedUntil.Value > expirationTime.Value));
                    if (strongerBan) this.invalidBlockHashesExpirations[hashBlock] = rejectedUntil;
                }
                else
                {
                    // No previous entry found, so we add a new ban. This means we can reach
                    // the capacity, in which case we first remove the oldest entry.
                    // Note that there can be entries in the circular array that are no longer in the dictionary.
                    // We skip all such entries.

                    // We start by adding the new entry to the queue, which will possibly remove the oldest entry if the capacity is reached.
                    // If capacity has not been reached, there is nothing more to do with the queue.
                    uint256 oldestEntry;
                    if (this.orderedHashList.Add(hashBlock, out oldestEntry))
                    {
                        // Then we check the dictionary whether it contains the removed entry.
                        // If not, we remove the next entry from the queue, if there is any,
                        // and we check the dictionary again. We repeat this until we find
                        // an existing entry or until we removed everything from the queue except
                        // for the entry we just added.
                        while (!this.invalidBlockHashesExpirations.Remove(oldestEntry))
                        {
                            if (this.orderedHashList.Count == 1)
                                break;

                            this.orderedHashList.RemoveFirst(out oldestEntry);
                        }
                    }

                    this.invalidBlockHashesExpirations.Add(hashBlock, rejectedUntil);
                }
            }
        }
    }
}
