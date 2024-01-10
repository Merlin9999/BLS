namespace BLS;

public interface IGlobber
{
    IEnumerable<Exception> IgnoredFileAccessExceptions { get; }
    IEnumerable<string> Execute();
}