using MediatR;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLS.Handlers
{
    public class GlobHelpHandler : IRequestHandler<GlobHelpArgs, EExitCode>
    {
        public Task<EExitCode> Handle(GlobHelpArgs request, CancellationToken cancellationToken)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/Merlin9999/DotNet.Glob/blob/develop/README.md",
                UseShellExecute = true
            });

            return Task.FromResult(EExitCode.Success);
        }
    }
}
