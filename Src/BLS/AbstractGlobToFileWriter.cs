namespace BLS;

public abstract class AbstractGlobToFileWriter<TArgs> : AbstractGlobWriter
    where TArgs : IGlobToWriteFileAndFactoryArgs
{
    protected readonly TArgs Args;

    protected AbstractGlobToFileWriter(TArgs args, TextWriter outputWriter) 
        : base(outputWriter)
    {
        this.Args = args;
    }

    public override async Task ExecuteAsync()
    {
        bool foundParentFolderInIncludes = this.Args.IncludeGlobPaths
            .Any(p => AbstractGlobber.SplitPathAndNormalizeRelativeSegments(p).StartsWith(new[] { ".." }));
        if (foundParentFolderInIncludes)
            throw new ArgumentException("Avoid using \"..\" folders in glob expressions when creating a zip file!");

        IGlobber globber = FileGlobberFactory.Create(this.Args);

        IEnumerable<string> files = globber.Execute();

        var comparer = this.Args.CaseSensitive
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;

        await this.WriteFilesAsync(files, comparer);

        await this.OutputIgnoredExceptionsAsync(globber.IgnoredAccessExceptions.ToList());
    }

    protected abstract Task WriteFilesAsync(IEnumerable<string> files, StringComparer comparer);
}