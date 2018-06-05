using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Broadcasting;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet.Interfaces
{
    public interface IGeneralPurposeWalletBroadcasterManager
    {
        Task BroadcastTransactionAsync(Transaction transaction);

        event EventHandler<TransactionBroadcastEntry> TransactionStateChanged;

        TransactionBroadcastEntry GetTransaction(uint256 transactionHash);

        void AddOrUpdate(Transaction transaction, State state, string ErrorMessage = "");
    }
}
