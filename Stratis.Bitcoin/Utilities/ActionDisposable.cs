using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    internal class ActionDisposable : IDisposable
    {
        Action onEnter, onLeave;
        public ActionDisposable(Action onEnter, Action onLeave)
        {
            this.onEnter = onEnter;
            this.onLeave = onLeave;
            onEnter();
        }

        #region IDisposable Members

        public void Dispose()
        {
            this.onLeave();
        }

        #endregion
    }
}
