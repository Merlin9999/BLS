using BLS.GlobWriters;
using MediatR;
using Serilog;

namespace BLS.Handlers;

public class ZipHandler(ILogger logger) : AbstractGlobberHandler, IRequestHandler<ZipArgs, EExitCode>
{
    public async Task<EExitCode> Handle(ZipArgs request, CancellationToken cancellationToken)
    {
        LogArgs(request, logger);

        // Make sure the zip file we are about to create is NOT included.
        request.ExcludeGlobPaths = request.ExcludeGlobPaths.Prepend(request.ZipFileName);

        GlobToZipWriter globToZipWriter = new GlobToZipWriter(request, Console.Out);
        await globToZipWriter.ExecuteAsync();

        return EExitCode.Success;
    }
}