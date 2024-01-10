using System.Collections.Immutable;
using CLConsole;

namespace LS.Test;

public class GlobberTestArgs : IGlobberArgs
{
    public IEnumerable<string> IncludeGlobPaths { get; set; } = ImmutableList<string>.Empty;
    public IEnumerable<string> ExcludeGlobPaths { get; set; } = ImmutableList<string>.Empty;
    public IEnumerable<string> BasePaths { get; set; } = ImmutableList<string>.Empty;
    public bool CaseSensitive { get; set; }
    public bool Sort { get; set; }
    public bool AllowDuplicatesWhenMultipleBasePaths { get; set; }
    public bool AbortOnFileSystemAccessExceptions { get; set; }
}