using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DBreeze;
using DBreeze.DataTypes;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public class PollsRepository : IDisposable
    {
        private readonly DBreezeEngine dbreeze;

        private readonly ILogger logger;

        private readonly DBreezeSerializer dBreezeSerializer;

        internal const string TableName = "DataTable";

        private static readonly byte[] RepositoryHighestIndexKey = new byte[0];

        private int highestPollId;

        public PollsRepository(DataFolder dataFolder, ILoggerFactory loggerFactory, DBreezeSerializer dBreezeSerializer)
            : this(dataFolder.PollsPath, loggerFactory, dBreezeSerializer)
        {
        }

        public PollsRepository(string folder, ILoggerFactory loggerFactory, DBreezeSerializer dBreezeSerializer)
        {
            Guard.NotEmpty(folder, nameof(folder));

            Directory.CreateDirectory(folder);
            this.dbreeze = new DBreezeEngine(folder);

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.dBreezeSerializer = dBreezeSerializer;
        }

        public void Initialize()
        {
            // Load highest index.
            this.highestPollId = -1;

            using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
            {
                Row<byte[], int> row = transaction.Select<byte[], int>(TableName, RepositoryHighestIndexKey);

                if (row.Exists)
                    this.highestPollId = row.Value;
            }

            this.logger.LogDebug("Polls repo initialized with highest id: {0}.", this.highestPollId);
        }

        /// <summary>Provides Id of the most recently added poll.</summary>
        public int GetHighestPollId()
        {
            return this.highestPollId;
        }

        private void SaveHighestPollId(DBreeze.Transactions.Transaction transaction)
        {
            transaction.Insert<byte[], int>(TableName, RepositoryHighestIndexKey, this.highestPollId);
        }

        /// <summary>Removes polls under provided ids.</summary>
        public void RemovePolls(params int[] ids)
        {
            using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
            {
                foreach (int pollId in ids.Reverse())
                {
                    if (this.highestPollId != pollId)
                        throw new ArgumentException("Only deletion of the most recent item is allowed!");

                    transaction.RemoveKey<byte[]>(TableName, pollId.ToBytes());

                    this.highestPollId--;
                    this.SaveHighestPollId(transaction);
                }

                transaction.Commit();
            }
        }

        /// <summary>Adds new poll.</summary>
        public void AddPolls(params Poll[] polls)
        {
            using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
            {
                foreach (Poll pollToAdd in polls)
                {
                    if (pollToAdd.Id != this.highestPollId + 1)
                        throw new ArgumentException("Id is incorrect. Gaps are not allowed.");

                    byte[] bytes = this.dBreezeSerializer.Serialize(pollToAdd);

                    transaction.Insert<byte[], byte[]>(TableName, pollToAdd.Id.ToBytes(), bytes);

                    this.highestPollId++;
                    this.SaveHighestPollId(transaction);
                }

                transaction.Commit();
            }
        }

        /// <summary>Updates existing poll.</summary>
        public void UpdatePoll(Poll poll)
        {
            using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
            {
                Row<byte[], byte[]> row = transaction.Select<byte[], byte[]>(TableName, poll.Id.ToBytes());

                if (!row.Exists)
                    throw new ArgumentException("Value doesn't exist!");

                byte[] bytes = this.dBreezeSerializer.Serialize(poll);

                transaction.Insert<byte[], byte[]>(TableName, poll.Id.ToBytes(), bytes);

                transaction.Commit();
            }
        }

        /// <summary>Loads polls under provided keys from the database.</summary>
        public List<Poll> GetPolls(params int[] ids)
        {
            using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
            {
                var polls = new List<Poll>(ids.Length);

                foreach (int id in ids)
                {
                    Row<byte[], byte[]> row = transaction.Select<byte[], byte[]>(TableName, id.ToBytes());

                    if (!row.Exists)
                        throw new ArgumentException("Value under provided key doesn't exist!");

                    Poll poll = this.dBreezeSerializer.Deserialize<Poll>(row.Value);

                    polls.Add(poll);
                }

                return polls;
            }
        }

        /// <summary>Loads all polls from the database.</summary>
        public List<Poll> GetAllPolls()
        {
            using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
            {
                var polls = new List<Poll>(this.highestPollId + 1);

                for (int i = 0; i < this.highestPollId + 1; i++)
                {
                    Row<byte[], byte[]> row = transaction.Select<byte[], byte[]>(TableName, i.ToBytes());

                    Poll poll = this.dBreezeSerializer.Deserialize<Poll>(row.Value);

                    polls.Add(poll);
                }

                return polls;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.dbreeze.Dispose();
        }
    }
}
