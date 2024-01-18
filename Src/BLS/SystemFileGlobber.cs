using System.Security;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace BLS;

public class SystemFileGlobber : AbstractGlobber
{
    private readonly Matcher _matcher;

    public SystemFileGlobber(IGlobberArgs args) 
        : base(args)
    {
        this._matcher = CreateMatcher(this.Args);
    }

    protected override IEnumerable<string> FindMatches(string basePath, IgnoredExceptionSet ignoredExceptions)
    {
        PatternMatchingResult files;

        try
        {
            files = this._matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(basePath)));
        }
        catch (SecurityException se) when (!this.Args.AbortOnFileSystemAccessExceptions)
        {
            ignoredExceptions.Add(se);
            yield break;
        }
        catch (UnauthorizedAccessException uae) when (!this.Args.AbortOnFileSystemAccessExceptions)
        {
            ignoredExceptions.Add(uae);
            yield break;
        }
        catch (DirectoryNotFoundException dnfe) when (!this.Args.AbortOnFileSystemAccessExceptions)
        {
            ignoredExceptions.Add(dnfe);
            yield break;
        }
        catch (IOException ioe) when (!this.Args.AbortOnFileSystemAccessExceptions)
        {
            ignoredExceptions.Add(ioe);
            yield break;
        }

        foreach (FilePatternMatch file in files.Files)
            yield return file.Path;
    }

    private static Matcher CreateMatcher(IGlobberArgs args)
    {
        StringComparison stringComparison = args.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var matcher = new Matcher(stringComparison);
        matcher.AddIncludePatterns(args.IncludeGlobPaths);
        matcher.AddExcludePatterns(args.ExcludeGlobPaths);
        return matcher;
    }
}