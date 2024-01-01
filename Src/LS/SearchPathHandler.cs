using MediatR;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CLConsole
{
    public class SearchPathHandler :AbstractGlobberHandler, IRequestHandler<SearchPathArgs, EExitCode>
    {
        private readonly ILogger _logger;

        public SearchPathHandler(ILogger logger)
        {
            this._logger = logger;
        }

        public async Task<EExitCode> Handle(SearchPathArgs request, CancellationToken cancellationToken)
        {
            InitBasePathsFromPathEnvironmentVar(request);
            LogPaths(request, this._logger);

            Globber globber = new Globber(request, Console.Out);
            await globber.ExecuteAsync();

            return EExitCode.Success;
        }

        private static void InitBasePathsFromPathEnvironmentVar(SearchPathArgs request)
        {
            string? envPath = Environment.GetEnvironmentVariable("PATH");
            request.BasePaths = envPath?.Split(Path.PathSeparator).ToList() ?? [];
        }
    }
}
