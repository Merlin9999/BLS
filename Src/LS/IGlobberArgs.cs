namespace CLConsole;

public interface IGlobberArgs
{
    IEnumerable<string> IncludeGlobPaths { get; set; }
    IEnumerable<string> ExcludeGlobPaths { get; set; }
    IEnumerable<string> BasePaths { get; set; }
    bool CaseSensitive { get; set; }
    bool Sort { get; set; }
    bool AllowDuplicates { get; set; }
    bool AbortOnFileSystemAccessExceptions { get; set; }
}