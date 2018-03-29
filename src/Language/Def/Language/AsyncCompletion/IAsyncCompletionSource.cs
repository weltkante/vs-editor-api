using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
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
        /// <param name="applicableSpan">Location where completion will take place, on the view's top buffer</param>
        /// <returns>A struct that holds completion items and applicable span</returns>
        Task<CompletionContext> GetCompletionContextAsync(CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableSpan, CancellationToken token);

        /// <summary>
        /// Returns tooltip associated with provided completion item.
        /// The returned object will be rendered by <see cref="IViewElementFactoryService"/>. See its documentation for supported types.
        /// Called on a background thread, so UIElement may not be returned.
        /// </summary>
        /// <param name="item"></param>
        /// <returns>Return type is pending what we agree to describe GUI in cross platform fashion</returns>
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
        /// <param name="applicableSpan">Applicable span for the prospective completion session. You may set it to default(SnapshotSpan) if returning false.</param>
        /// <returns>Whether completion should use the supplied applicable span.</returns>
        bool TryGetApplicableSpan(char typeChar, SnapshotPoint triggerLocation, out SnapshotSpan applicableSpan);
    }
}
