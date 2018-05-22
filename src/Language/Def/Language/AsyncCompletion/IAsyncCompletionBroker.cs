using System;
using System.Threading;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
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
    /// <remarks>
    /// This is a MEF component and may be imported by another MEF component:
    /// </remarks>
    /// <example>
    ///     [Import]
    ///     IAsyncCompletionBroker CompletionBroker;
    /// </example>
    public interface IAsyncCompletionBroker
    {
        /// <summary>
        /// Returns whether <see cref="IAsyncCompletionSession"/> is active in given <see cref="ITextView"/>.
        /// </summary>
        /// <remarks>
        /// The data may be stale if <see cref="IAsyncCompletionSession"/> was simultaneously dismissed on another thread.
        /// </remarks>
        /// <param name="textView">View that hosts completion and relevant buffers</param>
        bool IsCompletionActive(ITextView textView);

        /// <summary>
        /// Returns whether there are any completion item sources available for given <see cref="IContentType"/>.
        /// </summary>
        /// <param name="textView"><see cref="ITextView"/> to check for available completion source exports</param>
        bool IsCompletionSupported(IContentType contentType);

        /// <summary>
        /// Returns <see cref="IAsyncCompletionSession"/> if there is one active in a given <see cref="ITextView"/>, or null if not.
        /// </summary>
        /// <remarks>
        /// The data may be stale if <see cref="IAsyncCompletionSession"/> was simultaneously dismissed on another thread.
        /// Use <see cref="IAsyncCompletionSession.IsDismissed"/> to check state of returned session.
        /// </remarks>
        /// <param name="textView">View that hosts completion and relevant buffers</param>
        IAsyncCompletionSession GetSession(ITextView textView);

        /// <summary>
        /// Activates completion and returns <see cref="IAsyncCompletionSession"/>.
        /// If completion was already active, returns the existing session without changing it.
        /// Must be invoked on UI thread.
        /// This does not cause the completion popup to appear.
        /// To compute available icons and display the UI, call <see cref="IAsyncCompletionSession.OpenOrUpdate(InitialTrigger, SnapshotPoint, CancellationToken)"/>.
        /// </summary>
        /// <param name="textView">View that hosts completion and relevant buffers</param>
        /// <param name="triggerLocation">Location of completion on the view's data buffer: <see cref="ITextView.TextBuffer"/>. Used to pick relevant <see cref="IAsyncCompletionSource"/>s and <see cref="IAsyncCompletionItemManager"/></param>
        /// <param name="typeChar">Character that triggered completion, or default</param>
        /// <param name="token">Cancellation token that may interrupt this operation, despire running on the UI thread</param>
        IAsyncCompletionSession TriggerCompletion(ITextView textView, SnapshotPoint triggerLocation, char typedChar, CancellationToken token);

        /// <summary>
        /// Raised on UI thread when new <see cref="IAsyncCompletionSession"/> is triggered.
        /// </summary>
        event EventHandler<CompletionTriggeredEventArgs> CompletionTriggered;
    }
}
