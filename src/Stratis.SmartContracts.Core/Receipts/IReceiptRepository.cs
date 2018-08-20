using System.Collections.Generic;

namespace Stratis.SmartContracts.Core.Receipts
{
    public interface IReceiptRepository
    {
        void Store(IEnumerable<Receipt> receipts);
    }
}
