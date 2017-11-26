using System;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Helper class that is used for implementation of custom lock primitives.
    /// There are two actions - one is executed when an instance of the object is created
    /// and the other one is executed when the instance is disposed.
    /// </summary>
    internal class ActionDisposable : IDisposable
    {
        /// <summary>Method to call when an instance of the object is created.</summary>
        private Action onEnter;

        /// <summary>Method to call when an instance of the object is disposed.</summary>
        private Action onLeave;

        /// <summary>
        /// Initializes an instance of the object and executes the <paramref name="onEnter"/> method.
        /// </summary>
        /// <param name="onEnter">Method to call when an instance of the object is created.</param>
        /// <param name="onLeave">Method to call when an instance of the object is disposed.</param>
        public ActionDisposable(Action onEnter, Action onLeave)
        {
            this.onEnter = onEnter;
            this.onLeave = onLeave;
            onEnter();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.onLeave();
        }
    }
}
