using System.Collections.Immutable;
using MediatR;
using Serilog;

namespace CLConsole
{
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

            Globber globber = new Globber(request, Console.Out);
            await globber.ExecuteAsync();

            return EExitCode.Success;
        }
    }
}
