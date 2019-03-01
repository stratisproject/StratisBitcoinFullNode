using System.Collections.Generic;
using System.IO;
using System.Linq;
using DBreeze;
using DBreeze.DataTypes;
using DBreeze.Transactions;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.SourceChain;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    /// <summary>
    /// Holds information about all known deposits on the opposite chain.
    /// </summary>
    public class DepositRepository : IDepositRepository
    {
        /// <summary>This table contains the cross-chain transfer information.</summary>
        private const string DepositTableName = "deposits";

        /// <summary>The key of the counter-chain block tip that we have synced deposits to.</summary>
        private static readonly byte[] SyncedUpToBlockKey = new byte[] { 1 };

        private readonly DBreezeEngine db;
        private readonly DBreezeSerializer serializer;

        public DepositRepository(DataFolder dataFolder, IFederationGatewaySettings settings, DBreezeSerializer serializer)
        {
            string depositStoreName = "federatedTransfers" + settings.MultiSigAddress; // TODO: Unneccessary?
            string folder = Path.Combine(dataFolder.RootPath, depositStoreName);
            Directory.CreateDirectory(folder);
            this.db = new DBreezeEngine(folder);

            this.serializer = serializer;
        }

        /// <inheritdoc />
        public int GetSyncedBlockNumber()
        {
            using (Transaction dbreezeTransaction = this.db.GetTransaction())
            {
                Row<byte[], int> row = dbreezeTransaction.Select<byte[], int>(DepositTableName, SyncedUpToBlockKey);
                if (row.Exists)
                    return row.Value;

                return 0;
            }
        }

        public void SaveDeposits(IList<MaturedBlockDepositsModel> maturedBlockDeposits)
        {
            using (Transaction dbreezeTransaction = this.db.GetTransaction())
            {
                IEnumerable<IDeposit> allDeposits = maturedBlockDeposits.SelectMany(x => x.Deposits);

                foreach (IDeposit deposit in allDeposits)
                {
                    this.PutDeposit(dbreezeTransaction, (Deposit) deposit);
                }

                dbreezeTransaction.Commit();
            }
        }

        private void PutDeposit(Transaction dbreezeTransaction, Deposit deposit)
        {
            Guard.NotNull(deposit, nameof(deposit));

            byte[] depositBytes = this.serializer.Serialize(deposit);
            dbreezeTransaction.Insert<byte[], byte[]>(DepositTableName, deposit.Id.ToBytes(), depositBytes);
        }
    }
}
