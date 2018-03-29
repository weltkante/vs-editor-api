using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
{
    /// <summary>
    /// Represents a class that manages the completion feature.
    /// The editor uses this class to trigger completion and obtain instance of <see cref="IAsyncCompletionSession"/>
    /// which contains methods and events relevant to the active completion session.
    /// </summary>
    /// <example>
    ///     [Import]
    ///     IAsyncCompletionBroker CompletionBroker { get; set; }
    /// </example>
    public interface IAsyncCompletionBroker
    {
        /// <summary>
        /// Returns whether completion is active in given view
        /// </summary>
        /// <param name="textView">View that hosts completion and relevant buffers</param>
        bool IsCompletionActive(ITextView textView);

        /// <summary>
        /// Returns whether there are any completion item sources available
        /// for the top buffer in the provided view.
        /// </summary>
        /// <param name="textView">View to check for available completion source exports</param>
        bool IsCompletionSupported(IContentType contentType);

        /// <summary>
        /// Returns <see cref="IAsyncCompletionSession"/> if active or null if not
        /// </summary>
        /// <param name="textView">View that hosts completion and relevant buffers</param>
        IAsyncCompletionSession GetSession(ITextView textView);

        /// <summary>
        /// Activates completion and returns <see cref="IAsyncCompletionSession"/>.
        /// If completion was already active, returns the existing session without changing it.
        /// Must be invoked on UI thread.
        /// </summary>
        /// <param name="textView">View that hosts completion and relevant buffers</param>
        /// <param name="triggerLocation">Location of completion on the view's top buffer. Used to pick relevant <see cref="IAsyncCompletionSource"/>s and <see cref="IAsyncCompletionItemManager"/></param>
        /// <param name="typeChar">Character that triggered completion, or default</param>
        IAsyncCompletionSession TriggerCompletion(ITextView textView, SnapshotPoint triggerLocation, char typedChar);
    }
}
