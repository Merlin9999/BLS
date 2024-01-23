using BLS.GlobWriters;
using MediatR;
using Serilog;

namespace BLS.Handlers;

public class ListFilesHandler(ILogger logger) : AbstractGlobberHandler, IRequestHandler<ListFilesArgs, EExitCode>
{
    public async Task<EExitCode> Handle(ListFilesArgs request, CancellationToken cancellationToken)
    {
        LogArgs(request, logger);

        var globFilesToTextWriter = new GlobFilesToTextWriter(request, Console.Out);
        await globFilesToTextWriter.ExecuteAsync();

        return EExitCode.Success;
    }
}