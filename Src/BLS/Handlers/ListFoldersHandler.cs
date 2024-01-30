using BLS.GlobWriters;
using MediatR;
using Serilog;

namespace BLS.Handlers;

public class ListFoldersHandler(ILogger logger) : AbstractGlobberHandler, IRequestHandler<ListFoldersArgs, EExitCode>
{
    public async Task<EExitCode> Handle(ListFoldersArgs request, CancellationToken cancellationToken)
    {
        LogArgs(request, logger);

        var globFilesToTextWriter = new GlobFolderListToTextWriter(request, Console.Out);
        await globFilesToTextWriter.ExecuteAsync();

        return EExitCode.Success;
    }
}