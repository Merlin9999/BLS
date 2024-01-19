using BLS.Globbers;

namespace BLS.GlobWriters;

public class GlobFoldersToTextWriter : AbstractGlobWriter
{
    private readonly IGlobberArgs _args;

    public GlobFoldersToTextWriter(IGlobberArgs args, TextWriter outputWriter)
        : base(outputWriter)
    {
        this._args = args;
    }

    public override async Task ExecuteAsync()
    {
        IGlobber globber = new FolderGlobber(this._args);

        IEnumerable<string> folders = globber.Execute();
        foreach (string folder in folders)
            await this.OutputWriter.WriteLineAsync(folder);

        await this.OutputIgnoredExceptionsAsync(globber.IgnoredAccessExceptions.ToList());
    }
}