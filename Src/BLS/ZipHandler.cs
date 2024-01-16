using MediatR;
using Serilog;

namespace BLS;

public class ZipHandler : AbstractGlobberHandler, IRequestHandler<ZipArgs, EExitCode>
{
    private readonly ILogger _logger;

    public ZipHandler(ILogger logger)
    {
        this._logger = logger;
    }

    public async Task<EExitCode> Handle(ZipArgs request, CancellationToken cancellationToken)
    {
        LogArgs(request, this._logger);

        // Make sure the zip file we are about to create is NOT included.
        request.ExcludeGlobPaths = request.ExcludeGlobPaths.Prepend(request.ZipFileName);

        GlobToZip globToZip = new GlobToZip(request, Console.Out);
        await globToZip.ExecuteAsync();

        return EExitCode.Success;
    }
}