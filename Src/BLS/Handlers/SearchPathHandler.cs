using BLS.GlobWriters;
using MediatR;
using Serilog;

namespace BLS.Handlers;

public class SearchPathHandler(ILogger logger) : AbstractGlobberHandler, IRequestHandler<SearchPathArgs, EExitCode>
{
    public async Task<EExitCode> Handle(SearchPathArgs request, CancellationToken cancellationToken)
    {
        InitBasePathsFromPathEnvironmentVar(request);
        LogArgs(request, logger);

        GlobFileListToTextWriter globFilesToTextWriter = new GlobFileListToTextWriter(request, Console.Out);
        await globFilesToTextWriter.ExecuteAsync();

        return EExitCode.Success;
    }

    private static void InitBasePathsFromPathEnvironmentVar(SearchPathArgs request)
    {
        string? envPath = Environment.GetEnvironmentVariable("PATH");
        request.BasePaths = envPath?
            .Split(Path.PathSeparator)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList() ?? [];
    }
}