using System.Threading;

namespace AntlrParser.Tests
{
    public class ReaderWriterLockSlimAdapter : IReaderWriterLock
    {
        private readonly ReaderWriterLockSlim _lock;

        public ReaderWriterLockSlimAdapter(ReaderWriterLockSlim lockSlim)
        {
            _lock = lockSlim;
        }

        public void EnterWriteLock()
        {
            _lock.EnterWriteLock();
        }

        public void ExitWriteLock()
        {
            _lock.ExitWriteLock();
        }

        public void EnterReadLock()
        {
            _lock.EnterReadLock();
        }

        public void ExitReadLock()
        {
            _lock.ExitReadLock();
        }
    }
}