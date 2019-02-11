using System;
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
        }

        public int GetHighestPollId()
        {
            return this.highestPollId;
        }

        private void SaveHighestPollId(DBreeze.Transactions.Transaction transaction)
        {
            transaction.Insert<byte[], int>(TableName, RepositoryHighestIndexKey, this.highestPollId);
        }

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

        /// <inheritdoc />
        public void Dispose()
        {
            this.dbreeze.Dispose();
        }
    }
}
