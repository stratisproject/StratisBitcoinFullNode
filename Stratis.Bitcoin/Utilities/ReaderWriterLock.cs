using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{	
	internal class ReaderWriterLock
	{
		ReaderWriterLockSlim @lock = new ReaderWriterLockSlim();

		public IDisposable LockRead()
		{
			return new ActionDisposable(() => this.@lock.EnterReadLock(), () => this.@lock.ExitReadLock());
		}
		public IDisposable LockWrite()
		{
			return new ActionDisposable(() => this.@lock.EnterWriteLock(), () => this.@lock.ExitWriteLock());
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
