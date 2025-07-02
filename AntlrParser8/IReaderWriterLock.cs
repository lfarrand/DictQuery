namespace AntlrParser8;

public interface IReaderWriterLock
{
    void EnterWriteLock();
    void ExitWriteLock();
}