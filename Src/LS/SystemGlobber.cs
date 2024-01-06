using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using System.Security;

namespace CLConsole;

public class SystemGlobber : AbstractGlobber
{
    private readonly Matcher _matcher;

    public SystemGlobber(IGlobberArgs args) 
        : base(args)
    {
        this._matcher = CreateMatcher(this._args);
    }

    protected override IEnumerable<string> FindMatches(string basePath, List<Exception> ignoredFileAccessExceptions)
    {
        PatternMatchingResult files;

        try
        {
            files = this._matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(basePath)));
        }
        catch (SecurityException se) when (!this._args.AbortOnFileSystemAccessExceptions)
        {
            ignoredFileAccessExceptions.Add(se);
            yield break;
        }
        catch (UnauthorizedAccessException uae) when (!this._args.AbortOnFileSystemAccessExceptions)
        {
            ignoredFileAccessExceptions.Add(uae);
            yield break;
        }
        catch (DirectoryNotFoundException dnfe) when (!this._args.AbortOnFileSystemAccessExceptions)
        {
            ignoredFileAccessExceptions.Add(dnfe);
            yield break;
        }
        catch (IOException ioe) when (!this._args.AbortOnFileSystemAccessExceptions)
        {
            ignoredFileAccessExceptions.Add(ioe);
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