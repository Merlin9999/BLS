using BLS.Extensions;
using BLS.GlobWriters;
using MediatR;
using Serilog;

namespace BLS.Handlers;

public class CopyFilesHandler(ILogger logger) : AbstractGlobberHandler, IRequestHandler<CopyFilesArgs, EExitCode>
{
    public async Task<EExitCode> Handle(CopyFilesArgs request, CancellationToken cancellationToken)
    {
        LogArgs(request, logger);

        if (request.BasePath.IsParentPathContainingNestedPathOf(request.TargetFolder, request.CaseSensitive))
        {
            // Make sure the zip file we are about to create is NOT included.
            // A rooted exclusion path results in an error. So if rooted, we assume the target folder is not also contained within the base path.
            if (Path.IsPathRooted(request.TargetFolder))
            {
                string relativeTargetPath = Path.GetRelativePath(request.BasePath, request.TargetFolder);
                request.ExcludeGlobPaths = request.ExcludeGlobPaths.Prepend(relativeTargetPath);
            }
            else
            {
                request.ExcludeGlobPaths = request.ExcludeGlobPaths.Prepend(request.TargetFolder);
            }
        }

        var copyFilesWriter = new GlobAndCopyFilesWriter(request, Console.Out);
        await copyFilesWriter.ExecuteAsync();

        return EExitCode.Success;
    }
}
