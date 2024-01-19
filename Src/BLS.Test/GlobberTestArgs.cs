using System.Collections.Immutable;
using BLS.Globbers;

namespace BLS.Test;

public class GlobberTestArgs : IGlobberArgs
{
    public IEnumerable<string> IncludeGlobPaths { get; set; } = ImmutableList<string>.Empty;
    public IEnumerable<string> ExcludeGlobPaths { get; set; } = ImmutableList<string>.Empty;
    public IEnumerable<string> BasePaths { get; set; } = ImmutableList<string>.Empty;

    public bool UseFullyQualifiedPaths { get; set; }
    public bool CaseSensitive { get; set; }
    public bool Sort { get; set; }
    public bool AllowDuplicatesWhenMultipleBasePaths { get; set; }
    public bool AbortOnFileSystemAccessExceptions { get; set; }
}