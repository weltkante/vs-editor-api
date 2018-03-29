using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Implementation
{
    /// <summary>
    /// Internal item source used during lifetime of the suggestion mode item.
    /// </summary>
    internal class SuggestionModeCompletionItemSource : IAsyncCompletionSource
    {
        static IAsyncCompletionSource _instance;
        internal static IAsyncCompletionSource Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new SuggestionModeCompletionItemSource();
                return _instance;
            }
        }

        Task<CompletionContext> IAsyncCompletionSource.GetCompletionContextAsync(CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableSpan, CancellationToken token)
        {
            throw new NotImplementedException("This item source is not meant to be registered. It is used only to provide tooltip.");
        }

        Task<object> IAsyncCompletionSource.GetDescriptionAsync(CompletionItem item, CancellationToken token)
        {
            return Task.FromResult<object>(string.Empty);
        }

        bool IAsyncCompletionSource.TryGetApplicableSpan(char typeChar, SnapshotPoint triggerLocation, out SnapshotSpan applicableSpan)
        {
            applicableSpan = default(SnapshotSpan);
            return false;
        }
    }
}
