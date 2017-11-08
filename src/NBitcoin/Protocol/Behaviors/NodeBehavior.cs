using System;
using System.Collections.Generic;

namespace NBitcoin.Protocol.Behaviors
{
    public interface INodeBehavior
    {
        Node AttachedNode { get; }
        void Attach(Node node);
        void Detach();
        INodeBehavior Clone();
    }

    public abstract class NodeBehavior : INodeBehavior
    {
        private object cs = new object();
        private List<IDisposable> disposables = new List<IDisposable>();
        public Node AttachedNode { get; private set; }

        protected abstract void AttachCore();

        protected abstract void DetachCore();

        public abstract object Clone();

        protected void RegisterDisposable(IDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        public void Attach(Node node)
        {
            if (node == null)
                throw new ArgumentNullException("node");

            if (this.AttachedNode != null)
                throw new InvalidOperationException("Behavior already attached to a node");

            lock (this.cs)
            {
                this.AttachedNode = node;
                if (Disconnected(node))
                    return;

                this.AttachCore();
            }
        }

        protected void AssertNotAttached()
        {
            if (this.AttachedNode != null)
                throw new InvalidOperationException("Can't modify the behavior while it is attached");
        }

        private static bool Disconnected(Node node)
        {
            return (node.State == NodeState.Disconnecting) || (node.State == NodeState.Failed) || (node.State == NodeState.Offline);
        }

        public void Detach()
        {
            lock (this.cs)
            {
                if (this.AttachedNode == null)
                    return;

                this.DetachCore();
                foreach (IDisposable dispo in this.disposables)
                    dispo.Dispose();

                this.disposables.Clear();
                this.AttachedNode = null;
            }
        }

        INodeBehavior INodeBehavior.Clone()
        {
            return (INodeBehavior)Clone();
        }
    }
}