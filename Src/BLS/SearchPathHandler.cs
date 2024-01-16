using MediatR;
using Serilog;

namespace BLS;

public class SearchPathHandler : AbstractGlobberHandler, IRequestHandler<SearchPathArgs, EExitCode>
{
    private readonly ILogger _logger;

    public SearchPathHandler(ILogger logger)
    {
        this._logger = logger;
    }

    public async Task<EExitCode> Handle(SearchPathArgs request, CancellationToken cancellationToken)
    {
        InitBasePathsFromPathEnvironmentVar(request);
        LogArgs(request, this._logger);

        GlobToTextWriter globToTextWriter = new GlobToTextWriter(request, Console.Out);
        await globToTextWriter.ExecuteAsync();

        return EExitCode.Success;
    }

    private static void InitBasePathsFromPathEnvironmentVar(SearchPathArgs request)
    {
        string? envPath = Environment.GetEnvironmentVariable("PATH");
        request.BasePaths = envPath?
            .Split(Path.PathSeparator).Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList() ?? [];
    }
}