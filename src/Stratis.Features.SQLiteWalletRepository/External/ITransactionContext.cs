using System;

namespace Stratis.Features.SQLiteWalletRepository.External
{
    public interface ITransactionContext : IDisposable
    {
        void Rollback();
        void Commit();
    }
}
