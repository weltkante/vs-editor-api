using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Commanding
{
    internal interface IDynamicCommandHandler<T> where T : CommandArgs
    {
        bool CanExecuteCommand(T args);
    }
}
