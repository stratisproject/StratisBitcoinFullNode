using System;
using System.Collections.Immutable;
using System.Threading;

namespace Stratis.Bitcoin.Utilities
{
    public class Ambient<T>
    {
        public T Current => AmbientContext<T>.Current;
    }

    public class AmbientContextActionDisposable : IDisposable
    {
        private readonly Action action;

        public AmbientContextActionDisposable(Action action)
        {
            this.action = action;
        }

        public void Dispose()
        {
            this.action?.Invoke();
        }
    }

    public static class AmbientContext<T>
    {
        private static readonly AsyncLocal<ImmutableStack<T>> Store = new AsyncLocal<ImmutableStack<T>>();

        private static ImmutableStack<T> Stack
        {
            get => Store.Value ?? (Stack = ImmutableStack<T>.Empty);
            set => Store.Value = value;
        }

        public static T Current => Stack.IsEmpty ? default(T) : Stack.Peek();
        public static ImmutableStack<T> All => Stack;

        public static IDisposable Push(T instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            var current = Stack;
            Stack = current.Push(instance);
            return new AmbientContextActionDisposable(() => Stack = current);
        }

        public static IDisposable Suspend()
        {
            var current = Stack;
            Stack = ImmutableStack<T>.Empty;
            return new AmbientContextActionDisposable(() => Stack = current);
        }
    }
}
