using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
{
    /// <summary>
    /// Represents a class that filters and sorts available <see cref="CompletionItem"/>s given the current state of the editor.
    /// It also declares which completion filters are available for the returned subset of <see cref="CompletionItem"/>s.
    /// All methods are called on background thread.
    /// </summary>
    /// <remarks>
    /// Instances of this class should be created by <see cref="IAsyncCompletionItemManagerProvider"/>, which is a MEF part.
    /// </remarks>
    public interface IAsyncCompletionItemManager
    {
        /// <summary>
        /// This method is first called before completion is about to appear,
        /// and then on subsequent typing events and when user toggles completion filters.
        /// user's input tracked with <see cref="ITrackingSpan"/> on given <see cref="ITextSnapshot"/>
        /// and a collection of <see cref="CompletionFilterWithState"/>s that indicate user's filter selection.
        /// </summary>
        /// <param name="sortedList">Set of <see cref="CompletionItem"/>s to filter and sort, originally returned from <see cref="SortCompletionListAsync"/></param>
        /// <param name="triggerReason">The <see cref="CompletionTriggerReason"/> completion was initially triggered.</param>
        /// <param name="filterReason">The <see cref="CompletionFilterReason"/> completion is being updated.</param>
        /// <param name="snapshot">Text snapshot of the view's top buffer</param>
        /// <param name="applicableSpan">Span which tracks the location of the completion session and user's input</param>
        /// <param name="selectedFilters">Filters, their availability and selection state</param>
        /// <param name="view">Instance of <see cref="ITextView"/> that hosts the completion</param>
        /// <param name="token">Cancellation token that may interrupt this operation</param>
        /// <returns>Instance of <see cref="FilteredCompletionModel"/> that contains completion items to render, filters to display and recommended item to select</returns>
        Task<FilteredCompletionModel> UpdateCompletionListAsync(
            ImmutableArray<CompletionItem> sortedList,
            CompletionTriggerReason triggerReason,
            CompletionFilterReason filterReason,
            ITextSnapshot snapshot,
            ITrackingSpan applicableSpan,
            ImmutableArray<CompletionFilterWithState> selectedFilters,
            ITextView view,
            CancellationToken token);

        /// <summary>
        /// This method is first called before completion is about to appear,
        /// and then on subsequent typing events and when user toggles completion filters.
        /// The result of this method will be used in subsequent invocations of <see cref="UpdateCompletionListAsync"/>
        /// User's input is tracked by <see cref="ITrackingSpan"/> on a <see cref="ITextSnapshot"/> in a <see cref="ITextView"/>.
        /// </summary>
        /// <param name="initialList">Set of <see cref="CompletionItem"/>s to filter and sort</param>
        /// <param name="triggerReason">The <see cref="CompletionTriggerReason"/> completion was initially triggered.</param>
        /// <param name="snapshot">Text snapshot of the view's top buffer</param>
        /// <param name="applicableSpan">Span which tracks the location of the completion session and user's input</param>
        /// <param name="view">Instance of <see cref="ITextView"/> that hosts the completion</param>
        /// <param name="token">Cancellation token that may interrupt this operation</param>
        /// <returns>Instance of <see cref="FilteredCompletionModel"/> that contains completion items to render, filters to display and recommended item to select</returns>
        Task<ImmutableArray<CompletionItem>> SortCompletionListAsync(
            ImmutableArray<CompletionItem> initialList,
            CompletionTriggerReason triggerReason,
            ITextSnapshot snapshot,
            ITrackingSpan applicableSpan,
            ITextView view,
            CancellationToken token);
    }
}
