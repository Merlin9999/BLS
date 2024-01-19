using MediatR;
using Serilog;

namespace BLS;

public class ListFoldersHandler : AbstractGlobberHandler, IRequestHandler<ListFoldersArgs, EExitCode>
{
    private readonly ILogger _logger;

    public ListFoldersHandler(ILogger logger)
    {
        this._logger = logger;
    }

    public async Task<EExitCode> Handle(ListFoldersArgs request, CancellationToken cancellationToken)
    {
        LogArgs(request, this._logger);

        var globFilesToTextWriter = new GlobFoldersToTextWriter(request, Console.Out);
        await globFilesToTextWriter.ExecuteAsync();

        return EExitCode.Success;
    }
}