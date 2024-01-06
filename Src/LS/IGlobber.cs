using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CLConsole;

public interface IGlobber
{
    IEnumerable<Exception> IgnoredFileAccessExceptions { get; }
    IAsyncEnumerable<string> ExecuteAsync();
}