using MediatR;
using Serilog;

namespace BLS;

public class CopyFilesHandler : AbstractGlobberHandler, IRequestHandler<CopyFilesArgs, EExitCode>
{
    private readonly ILogger _logger;

    public CopyFilesHandler(ILogger logger)
    {
        this._logger = logger;
    }

    public async Task<EExitCode> Handle(CopyFilesArgs request, CancellationToken cancellationToken)
    {
        LogArgs(request, this._logger);

        // Make sure the zip file we are about to create is NOT included.
        request.ExcludeGlobPaths = request.ExcludeGlobPaths.Prepend(request.TargetFolder);

        var copyFilesWriter = new GlobAndCopyFilesWriter(request, Console.Out);
        await copyFilesWriter.ExecuteAsync();

        return EExitCode.Success;
    }
}