using MediatR;
using Serilog;

namespace CLConsole
{
    public class ListFilesHandler : IRequestHandler<ListFilesArgs, int>
    {
        private readonly ILogger _logger;

        public ListFilesHandler(ILogger logger)
        {
            this._logger = logger;
        }

        public async Task<int> Handle(ListFilesArgs request, CancellationToken cancellationToken)
        {
            this.LogPaths(request);

            Globber globber = new Globber(request, Console.Out);
            await globber.ExecuteAsync();

            return 0;
        }

        private void LogPaths(ListFilesArgs request)
        {
            List<string> includeGlobPaths = request.IncludeGlobPaths.ToList();
            List<string> excludeGlobPaths = request.ExcludeGlobPaths.ToList();

            this._logger.Debug(includeGlobPaths.Count > 0 ? "Included Glob Paths:" : "No Include Glob Paths!");
            for (var index = 0; index < includeGlobPaths.Count; index++)
            {
                string globPath = includeGlobPaths[index];
                this._logger.Debug($"   {index:D2} - \"{globPath}\"");
            }

            this._logger.Debug(excludeGlobPaths.Count > 0 ? "Excluded Glob Paths:" : "No Exclude Glob Paths.");
            for (var index = 0; index < excludeGlobPaths.Count; index++)
            {
                string globPath = excludeGlobPaths[index];
                this._logger.Debug($"   {index:D2} - \"{globPath}\"");
            }
        }
    }
}
