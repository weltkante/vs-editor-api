using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Implementation
{
    internal interface ICompletionComputationCallbackHandler<TModel>
    {
        Task UpdateUi(TModel model, CancellationToken token);
        void Dismiss();
    }
}
