using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
{
    /// <summary>
    /// Represents a class that provides <see cref="CompletionItem"/>s and other information
    /// relevant to the completion feature at a specific <see cref="SnapshotPoint"/>.
    /// </summary>
    /// <remarks>
    /// Instances of this class should be created by <see cref="IAsyncCompletionSourceProvider"/>, which is a MEF part.
    /// </remarks>
    public interface IAsyncCompletionSource
    {
        /// <summary>
        /// Called once per completion session to fetch the set of all completion items available at a given location.
        /// Called on a background thread.
        /// </summary>
        /// <param name="trigger">What caused the completion</param>
        /// <param name="triggerLocation">Location where completion was triggered, on the subject buffer that matches this <see cref="IAsyncCompletionSource"/>'s content type</param>
        /// <param name="applicableSpan">Location where completion will take place, on the view's data buffer: <see cref="ITextView.TextBuffer"/></param>
        /// <param name="token">Cancellation token that may interrupt this operation</param>
        /// <returns>A struct that holds completion items and applicable span</returns>
        Task<CompletionContext> GetCompletionContextAsync(InitialTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableSpan, CancellationToken token);

        /// <summary>
        /// Returns tooltip associated with provided <see cref="CompletionItem"/>.
        /// The returned object will be rendered by <see cref="IViewElementFactoryService"/>. See its documentation for default supported types.
        /// You may export a <see cref="IViewElementFactory"/> to provide a renderer for a custom type.
        /// Since this method is called on a background thread and on multiple platforms, an instance of UIElement may not be returned.
        /// </summary>
        /// <param name="item"><see cref="CompletionItem"/> which is a subject of the tooltip</param>
        /// <param name="token">Cancellation token that may interrupt this operation</param>
        /// <returns>An object that will be passed to <see cref="IViewElementFactoryService"/>. See its documentation for supported types.</returns>
        Task<object> GetDescriptionAsync(CompletionItem item, CancellationToken token);

        /// <summary>
        /// Provides the span applicable to the prospective session.
        /// Called on UI thread and expected to return very quickly, based on syntactic information.
        /// This method is called sequentially on available <see cref="IAsyncCompletionSource"/>s until one of them returns true.
        /// Returning false does not exclude this source from participating in completion session.
        /// If no <see cref="IAsyncCompletionSource"/>s return true, there will be no completion session.
        /// </summary>
        /// <param name="typeChar">Character typed by the user</param>
        /// <param name="triggerLocation">Location on the subject buffer that matches this <see cref="IAsyncCompletionSource"/>'s content type</param>
        /// <param name="applicableToSpan">Applicable span for the prospective completion session. You may set it to default(SnapshotSpan) if returning false.</param>
        /// <param name="token">Cancellation token that may interrupt this operation, despite it running on the UI thread.</param>
        /// <returns>Whether completion should use the supplied applicable span.</returns>
        bool TryGetApplicableToSpan(char typeChar, SnapshotPoint triggerLocation, out SnapshotSpan applicableToSpan, CancellationToken token);
    }
}
