using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Interface for object that can be initialized.
    /// </summary>
    public interface IInitializable
    {
        Task InitializeAsync();
    }
}
