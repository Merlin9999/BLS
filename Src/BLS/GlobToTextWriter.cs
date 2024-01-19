namespace BLS;

public class GlobToTextWriter : AbstractGlobWriter
{
    private readonly IGlobberAndFactoryArgs _args;

    public GlobToTextWriter(IGlobberAndFactoryArgs args, TextWriter outputWriter)
        : base(outputWriter)
    {
        this._args = args;
    }

    public override async Task ExecuteAsync()
    {
        IGlobber globber = FileGlobberFactory.Create(this._args);

        IEnumerable<string> files = globber.Execute();
        foreach (string file in files)
            await this.OutputWriter.WriteLineAsync(file);

        await this.OutputIgnoredExceptionsAsync(globber.IgnoredAccessExceptions.ToList());
    }
}