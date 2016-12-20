using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
			onLeave();
		}

		#endregion
	}
	internal class ReaderWriterLock
	{
		ReaderWriterLockSlim @lock = new ReaderWriterLockSlim();

		public IDisposable LockRead()
		{
			return new ActionDisposable(() => @lock.EnterReadLock(), () => @lock.ExitReadLock());
		}
		public IDisposable LockWrite()
		{
			return new ActionDisposable(() => @lock.EnterWriteLock(), () => @lock.ExitWriteLock());
		}

		internal bool TryLockWrite(out IDisposable locked)
		{
			locked = null;
			if(this.@lock.TryEnterWriteLock(0))
			{
				locked = new ActionDisposable(() =>
				{
				}, () => this.@lock.ExitWriteLock());
				return true;
			}
			return false;
		}
	}
}
