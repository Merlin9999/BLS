using System.Collections.Immutable;
using System.Security;
using DotNet.Globbing;

namespace CLConsole;

public class ImprovedGlobber : AbstractGlobber
{
    // Derived from: https://stackoverflow.com/a/54300816/677612

    // Check out: https://stackoverflow.com/a/34580159/677612

    public ImprovedGlobber(IGlobberArgs args) 
        : base(args)
    {
    }

    protected override IEnumerable<string> FindMatches(string basePath, List<Exception> ignoredFileAccessExceptions)
    {
        ImmutableList<Glob> includeGlobs = this._args.IncludeGlobPaths.Select(g => Glob.Parse(g)).ToImmutableList();
        ImmutableList<Glob> excludeGlobs = this._args.ExcludeGlobPaths.Select(g => Glob.Parse(g)).ToImmutableList();

        var baseDir = new DirectoryInfo(basePath);

        foreach (FileInfo fileInfo in this.EnumerateAllFiles(baseDir, excludeGlobs, ignoredFileAccessExceptions))
        {
            if (includeGlobs.Any(glob => glob.IsMatch(fileInfo.FullName)) && !excludeGlobs.Any(glob => glob.IsMatch(fileInfo.FullName)))
                yield return fileInfo.FullName;
        }
    }

    private IEnumerable<FileInfo> EnumerateAllFiles(DirectoryInfo dirInfo, ImmutableList<Glob> excludeGlobs, List<Exception> ignoredFileAccessExceptions)
    {
        ImmutableList<FileInfo> files = this.DoFileSysOp(
            () => dirInfo.EnumerateFiles().ToImmutableList(),
            () => ImmutableList<FileInfo>.Empty,
            ignoredFileAccessExceptions);

        foreach (FileInfo fileInfo in files)
            yield return fileInfo;

        ImmutableList<DirectoryInfo> subDirInfos = this.DoFileSysOp(
            () => dirInfo.EnumerateDirectories().ToImmutableList(),
            () => ImmutableList<DirectoryInfo>.Empty,
            ignoredFileAccessExceptions);

        foreach (DirectoryInfo subDirInfo in subDirInfos)
        {
            if (excludeGlobs.Any(glob => glob.IsMatch(subDirInfo.FullName)))
                continue;

            foreach (FileInfo fileInfo in this.EnumerateAllFiles(subDirInfo, excludeGlobs, ignoredFileAccessExceptions))
                yield return fileInfo;
        }
    }

    private T DoFileSysOp<T>(Func<T> operation, Func<T> getDefault, List<Exception> ignoredFileAccessExceptions)
    {
        try
        {
            return operation();
        }
        catch (SecurityException se) when (!this._args.AbortOnFileSystemAccessExceptions)
        {
            ignoredFileAccessExceptions.Add(se);
            return getDefault();
        }
        catch (UnauthorizedAccessException uae) when (!this._args.AbortOnFileSystemAccessExceptions)
        {
            ignoredFileAccessExceptions.Add(uae);
            return getDefault();
        }
        catch (DirectoryNotFoundException dnfe) when (!this._args.AbortOnFileSystemAccessExceptions)
        {
            ignoredFileAccessExceptions.Add(dnfe);
            return getDefault();
        }
        catch (IOException ioe) when (!this._args.AbortOnFileSystemAccessExceptions)
        {
            ignoredFileAccessExceptions.Add(ioe);
            return getDefault();
        }
    }
}