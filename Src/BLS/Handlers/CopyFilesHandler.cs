using BLS.GlobWriters;
using MediatR;
using Serilog;

namespace BLS.Handlers;

public class CopyFilesHandler(ILogger logger) : AbstractGlobberHandler, IRequestHandler<CopyFilesArgs, EExitCode>
{
    public async Task<EExitCode> Handle(CopyFilesArgs request, CancellationToken cancellationToken)
    {
        LogArgs(request, logger);

        // Make sure the zip file we are about to create is NOT included.
        request.ExcludeGlobPaths = request.ExcludeGlobPaths.Prepend(request.TargetFolder);

        var copyFilesWriter = new GlobAndCopyFilesWriter(request, Console.Out);
        await copyFilesWriter.ExecuteAsync();

        return EExitCode.Success;
    }
}