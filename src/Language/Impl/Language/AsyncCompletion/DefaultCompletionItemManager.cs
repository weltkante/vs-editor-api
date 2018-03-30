using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.PatternMatching;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Implementation
{
    [Export(typeof(IAsyncCompletionItemManagerProvider))]
    [Name(PredefinedCompletionNames.DefaultCompletionItemManager)]
    [ContentType("text")]
    internal class DefaultCompletionItemManagerProvider : IAsyncCompletionItemManagerProvider
    {
        [Import]
        public IPatternMatcherFactory PatternMatcherFactory;

        DefaultCompletionItemManager _instance;

        IAsyncCompletionItemManager IAsyncCompletionItemManagerProvider.GetOrCreate(ITextView textView)
        {
            if (_instance == null)
                _instance = new DefaultCompletionItemManager(PatternMatcherFactory);
            return _instance;
        }
    }

    internal class DefaultCompletionItemManager : IAsyncCompletionItemManager
    {
        readonly IPatternMatcherFactory _patternMatcherFactory;

        internal DefaultCompletionItemManager(IPatternMatcherFactory patternMatcherFactory)
        {
            _patternMatcherFactory = patternMatcherFactory;
        }

        Task<FilteredCompletionModel> IAsyncCompletionItemManager.UpdateCompletionListAsync(
            ImmutableArray<CompletionItem> sortedList, CompletionTriggerReason triggerReason, CompletionFilterReason filterReason,
            ITextSnapshot snapshot, ITrackingSpan applicableSpan, ImmutableArray<CompletionFilterWithState> filters, ITextView view, CancellationToken token)
        {
            // Filter by text
            var filterText = applicableSpan.GetText(snapshot);
            if (string.IsNullOrWhiteSpace(filterText))
            {
                // There is no text filtering. Just apply user filters, sort alphabetically and return.
                IEnumerable<CompletionItem> listFiltered = sortedList;
                if (filters.Any(n => n.IsSelected))
                {
                    listFiltered = sortedList.Where(n => ShouldBeInCompletionList(n, filters));
                }
                var listSorted = listFiltered.OrderBy(n => n.SortText);
                var listHighlighted = listSorted.Select(n => new CompletionItemWithHighlight(n)).ToImmutableArray();
                return Task.FromResult(new FilteredCompletionModel(listHighlighted, 0, filters));
            }

            // Pattern matcher not only filters, but also provides a way to order the results by their match quality.
            // The relevant CompletionItem is match.Item1, its PatternMatch is match.Item2
            var patternMatcher = _patternMatcherFactory.CreatePatternMatcher(
                filterText,
                new PatternMatcherCreationOptions(System.Globalization.CultureInfo.CurrentCulture, PatternMatcherCreationFlags.IncludeMatchedSpans));

            var matches = sortedList
                // Perform pattern matching
                .Select(completionItem => (completionItem, patternMatcher.TryMatch(completionItem.FilterText)))
                // Pick only items that were matched, unless length of filter text is 1
                .Where(n => (filterText.Length == 1 || n.Item2.HasValue));

            // See which filters might be enabled based on the typed code
            var textFilteredFilters = matches.SelectMany(n => n.Item1.Filters).Distinct();

            // When no items are available for a given filter, it becomes unavailable
            var updatedFilters = ImmutableArray.CreateRange(filters.Select(n => n.WithAvailability(textFilteredFilters.Contains(n.Filter))));

            // Filter by user-selected filters. The value on availableFiltersWithSelectionState conveys whether the filter is selected.
            var filterFilteredList = matches;
            if (filters.Any(n => n.IsSelected))
            {
                filterFilteredList = matches.Where(n => ShouldBeInCompletionList(n.Item1, filters));
            }

            var bestMatch = filterFilteredList.OrderByDescending(n => n.Item2.HasValue).ThenBy(n => n.Item2).FirstOrDefault();
            var listWithHighlights = filterFilteredList.Select(n => n.Item2.HasValue ? new CompletionItemWithHighlight(n.Item1, n.Item2.Value.MatchedSpans) : new CompletionItemWithHighlight(n.Item1)).ToImmutableArray();

            int selectedItemIndex = 0;
            for (int i = 0; i < listWithHighlights.Length; i++)
            {
                if (listWithHighlights[i].CompletionItem == bestMatch.Item1)
                {
                    selectedItemIndex = i;
                    break;
                }
            }

            return Task.FromResult(new FilteredCompletionModel(listWithHighlights, selectedItemIndex, updatedFilters));
        }

        Task<ImmutableArray<CompletionItem>> IAsyncCompletionItemManager.SortCompletionListAsync(
            ImmutableArray<CompletionItem> initialList, CompletionTriggerReason triggerReason, ITextSnapshot snapshot,
            ITrackingSpan applicableSpan, ITextView view, CancellationToken token)
        {
            return Task.FromResult(initialList.OrderBy(n => n.SortText).ToImmutableArray());
        }

        #region Filtering

        private static bool ShouldBeInCompletionList(
            CompletionItem item,
            ImmutableArray<CompletionFilterWithState> filtersWithState)
        {
            foreach (var filterWithState in filtersWithState.Where(n => n.IsSelected))
            {
                if (item.Filters.Any(n => n == filterWithState.Filter))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion
    }
}
