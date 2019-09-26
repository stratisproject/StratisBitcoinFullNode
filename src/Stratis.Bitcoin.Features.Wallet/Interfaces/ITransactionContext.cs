using System;

namespace Stratis.Bitcoin.Features.Wallet.Interfaces
{
    public interface ITransactionContext : IDisposable
    {
        void Rollback();
        void Commit();
    }
}
