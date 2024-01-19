using MediatR;
using Serilog;

namespace BLS;

public class ListFilesHandler : AbstractGlobberHandler, IRequestHandler<ListFilesArgs, EExitCode>
{
    private readonly ILogger _logger;

    public ListFilesHandler(ILogger logger)
    {
        this._logger = logger;
    }

    public async Task<EExitCode> Handle(ListFilesArgs request, CancellationToken cancellationToken)
    {
        LogArgs(request, this._logger);

        var globFilesToTextWriter = new GlobFilesToTextWriter(request, Console.Out);
        await globFilesToTextWriter.ExecuteAsync();

        return EExitCode.Success;
    }
}