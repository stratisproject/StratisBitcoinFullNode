using System;

namespace NBitcoin
{
    internal class ActionDisposable : IDisposable
    {
        private Action onEnter, onLeave;
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
