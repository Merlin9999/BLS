namespace BLS.Globbers;

public interface IGlobToWriteFileAndFactoryArgs : IGlobberAndFactoryArgs, IGlobToWriteFile, IGlobberFactoryArgs
{
}

public interface IGlobToWriteFile : IGlobberArgs
{
    string BasePath { get; set; }
    bool ReplaceOnDuplicate { get; set; }
    bool ErrorOnDuplicate { get; set; }
}

public interface IGlobberAndFactoryArgs : IGlobberArgs, IGlobberFactoryArgs
{
}

public interface IGlobberArgs
{
    IEnumerable<string> IncludeGlobPaths { get; set; }
    IEnumerable<string> ExcludeGlobPaths { get; set; }
    IEnumerable<string> BasePaths { get; set; }
    bool UseFullyQualifiedPaths { get; set; }
    bool CaseSensitive { get; set; }
    bool Sort { get; set; }
    bool AllowDuplicatesWhenMultipleBasePaths { get; set; }
    bool AbortOnFileSystemAccessExceptions { get; set; }
}

public interface IGlobberFactoryArgs
{
    bool UseFrameworkGlobber { get; set; }
}
