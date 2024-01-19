namespace BLS;

public interface IGlobber
{
    IEnumerable<Exception> IgnoredAccessExceptions { get; }
    IEnumerable<string> Execute();
}