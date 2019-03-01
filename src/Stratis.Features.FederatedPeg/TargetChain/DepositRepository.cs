using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using DBreeze.Transactions;
using Stratis.Bitcoin.Configuration;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;

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

        public DepositRepository(DataFolder dataFolder, IFederationGatewaySettings settings)
        {
            string depositStoreName = "federatedTransfers" + settings.MultiSigAddress; // TODO: Unneccessary?
            string folder = Path.Combine(dataFolder.RootPath, depositStoreName);
            Directory.CreateDirectory(folder);
            this.db = new DBreezeEngine(folder);
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

        public async Task<bool> SaveDepositsAsync(IList<MaturedBlockDepositsModel> maturedBlockDeposits)
        {
            throw new NotImplementedException();
        }
    }
}
