using System.Collections.Immutable;
using DotNet.Globbing;

namespace BLS.Globbers;

public class FolderGlobber(IGlobberArgs args) : AbstractImprovedGlobber<DirectoryInfo>(args)
{
    protected override IEnumerable<DirectoryInfo> EnumerateAllEntries(DirectoryInfo dirInfo, ImmutableList<IncludeGlobber> includeInfos,
        ImmutableList<Glob> excludeGlobs, DirectoryInfo commonRootDir, DirectoryInfo baseDir, IgnoredExceptionSet ignoredExceptions)
    {
        return this.EnumerateAllFolders(dirInfo, includeInfos, excludeGlobs, commonRootDir, baseDir, ignoredExceptions);
    }

    protected IEnumerable<DirectoryInfo> EnumerateAllFolders(DirectoryInfo dirInfo, ImmutableList<IncludeGlobber> includeInfos,
        ImmutableList<Glob> excludeGlobs, DirectoryInfo commonRootDir, DirectoryInfo baseDir, IgnoredExceptionSet ignoredExceptions)
    {
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
                yield return subDirInfo;

                // Not sure if it is faster to potentially try to match against extra Globs, or build a filtered Glob list for each subdirectory.
                ImmutableList<IncludeGlobber> subDirIncludeInfos = includeInfos;
                //.Where(include => include.IsFolderMatch(fullFolderNamePath))
                //.ToImmutableList();

                foreach (DirectoryInfo subfolderDirInfo in this.EnumerateAllFolders(subDirInfo, subDirIncludeInfos, 
                             excludeGlobs, commonRootDir, baseDir, ignoredExceptions))
                    yield return subfolderDirInfo;
            }
        }
    }
}