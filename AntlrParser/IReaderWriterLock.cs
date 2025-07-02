namespace AntlrParser
{
    public interface IReaderWriterLock
    {
        void EnterWriteLock();
        void ExitWriteLock();
    }
}