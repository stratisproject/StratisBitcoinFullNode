using System.Collections.Generic;

namespace Stratis.SmartContracts.Core.Receipts
{
    public interface IReceiptRepository
    {
        /// <summary>
        /// Permanently store several receipts.
        /// </summary>
        void Store(IEnumerable<Receipt> receipts);
    }
}
