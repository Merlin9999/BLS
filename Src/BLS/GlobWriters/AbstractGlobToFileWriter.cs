using BLS.Globbers;

namespace BLS.GlobWriters;

public abstract class AbstractGlobToFileWriter<TArgs>(TArgs args, TextWriter outputWriter)
    : AbstractGlobWriter(outputWriter)
    where TArgs : IGlobToWriteFileAndFactoryArgs
{
    protected readonly TArgs Args = args;

    public override async Task ExecuteAsync()
    {
        bool foundParentFolderInIncludes = this.Args.IncludeGlobPaths
            .Any(p => AbstractGlobber.SplitPathAndNormalizeRelativeSegments(p).StartsWith(ParentFolderAsArray));
        if (foundParentFolderInIncludes)
            throw new ArgumentException("Avoid using \"..\" folders in glob expressions when creating a zip file or copying files!");

        IGlobber<FilePathInfo, FileInfo> globber = FileGlobberFactory.Create(this.Args);

        IEnumerable<FilePathInfo> files = globber.Execute();

        var comparer = this.Args.CaseSensitive
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;

        await this.WriteFilesAsync(files, comparer);

        await this.OutputIgnoredExceptionsAsync(globber.IgnoredAccessExceptions.ToList());
    }

    protected abstract Task WriteFilesAsync(IEnumerable<FilePathInfo> files, StringComparer comparer);
}