using System.Collections.Immutable;
using System.IO;
using DotNet.Globbing;

namespace BLS.Globbers;

public class FileGlobber(IGlobberArgs args) : AbstractImprovedGlobber<FilePathInfo, FileInfo>(args)
{
    protected override IEnumerable<FileInfo> EnumerateAllEntries(DirectoryInfo dirInfo, ImmutableList<IncludeGlobber> includeInfos, 
        ImmutableList<Glob> excludeGlobs, DirectoryInfo commonRootDir, DirectoryInfo baseDir, IgnoredExceptionSet ignoredExceptions)
    {
        return this.EnumerateAllFiles(dirInfo, includeInfos, excludeGlobs, commonRootDir, baseDir, ignoredExceptions);
    }

    protected IEnumerable<FileInfo> EnumerateAllFiles(DirectoryInfo dirInfo, ImmutableList<IncludeGlobber> includeInfos,
        ImmutableList<Glob> excludeGlobs, DirectoryInfo commonRootDir, DirectoryInfo baseDir, IgnoredExceptionSet ignoredExceptions)
    {
        ImmutableList<FileInfo> files = this.DoFileSysOp(
            () => dirInfo.EnumerateFiles().ToImmutableList(),
            () => ImmutableList<FileInfo>.Empty,
            ignoredExceptions);

        foreach (FileInfo fileInfo in files)
            yield return fileInfo;

        ImmutableList<DirectoryInfo> subDirInfos = this.DoFileSysOp(
            () => dirInfo.EnumerateDirectories().ToImmutableList(),
            () => ImmutableList<DirectoryInfo>.Empty,
            ignoredExceptions);

        foreach (DirectoryInfo subDirInfo in subDirInfos)
        {
            string fullFolderNamePath = BuildRelativeName(subDirInfo.FullName, commonRootDir, baseDir);

            if (excludeGlobs.Any(glob => glob.IsMatch(fullFolderNamePath)))
                continue;

            if (includeInfos.Any(include => include.IsFolderMatch(fullFolderNamePath) || include.IsRecursFolder(fullFolderNamePath)))
            {
                // Not sure if it is faster to potentially try to match against extra Globs, or build a filtered Glob list for each subdirectory.
                ImmutableList<IncludeGlobber> subDirIncludeInfos = includeInfos;
                //.Where(include => include.IsFolderMatch(fullFolderNamePath))
                //.ToImmutableList();

                foreach (FileInfo fileInfo in this.EnumerateAllFiles(subDirInfo, subDirIncludeInfos,
                             excludeGlobs, commonRootDir, baseDir, ignoredExceptions))
                    yield return fileInfo;
            }
        }
    }

    protected override FilePathInfo CreateOutputEntry(string basePath, string entryPath, FileInfo? fileSysInfo)
    {
        if (fileSysInfo is null)
            throw new ArgumentNullException(nameof(fileSysInfo));

        return new FilePathInfo()
        {
            BasePath = basePath,
            EntryPath = this.UseFullyQualifiedOutputPaths
                ? Path.GetFullPath(Path.Combine(this.CurrentWorkingDirectory, basePath, entryPath))
                : entryPath,
            EntryInfo = fileSysInfo,
        };
    }
}