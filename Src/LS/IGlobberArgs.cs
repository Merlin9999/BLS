namespace CLConsole;

public interface IGlobberAndFactoryArgs : IGlobberArgs, IGlobberFactoryArgs
{
}

public interface IGlobberArgs
{
    IEnumerable<string> IncludeGlobPaths { get; set; }
    IEnumerable<string> ExcludeGlobPaths { get; set; }
    IEnumerable<string> BasePaths { get; set; }
    bool CaseSensitive { get; set; }
    bool Sort { get; set; }
    bool AllowDuplicatesWhenMultipleBasePaths { get; set; }
    bool AbortOnFileSystemAccessExceptions { get; set; }
}

public interface IGlobberFactoryArgs
{
    bool UseFrameworkGlobber { get; set; }
}
